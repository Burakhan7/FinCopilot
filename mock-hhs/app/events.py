"""Olay bildirimi: olay üretme, tarama ve YÖS'e iletim (outbox + yeniden deneme).

Olay tipleri (ÖHVPS):
- KAYNAK_GUNCELLENDI   + kaynakTipi BAKIYE (izin 06), HESAP_BILGISI_RIZASI
- AYRIK_GKD_BASARILI   + kaynakTipi HESAP_BILGISI_RIZASI
- AYRIK_GKD_BASARISIZ  + kaynakTipi HESAP_BILGISI_RIZASI

Not: Olay gövdesinin alan adları spesifikasyonla ayrıca karşılaştırılmalı.
"""
from __future__ import annotations

import asyncio
import logging
import uuid
from datetime import datetime, timezone

import httpx
from sqlalchemy import select
from sqlalchemy.orm import Session

from . import config
from .config import TR_TZ
from .db import Consent, Event, ScanState, SessionLocal, Subscription

log = logging.getLogger("mock_hhs.events")


def now() -> datetime:
    return datetime.now(timezone.utc)


def aware(dt):
    return dt.replace(tzinfo=timezone.utc) if dt is not None and dt.tzinfo is None else dt


# ------------------------------------------------------------------ üretme
def subscription_for(db: Session, hhs: str, yos: str, olay_tipi: str, kaynak_tipi: str) -> Subscription | None:
    for s in db.scalars(select(Subscription).where(Subscription.hhs_kod == hhs, Subscription.yos_kod == yos)):
        if any(t["olayTipi"] == olay_tipi and t["kaynakTipi"] == kaynak_tipi for t in s.abonelik_tipleri):
            return s
    return None


def emit(db: Session, c: Consent, olay_tipi: str, kaynak_tipi: str, kaynak_no: str) -> Event | None:
    """Abonelik varsa olayı outbox'a yaz. Commit çağırana aittir."""
    s = subscription_for(db, c.hhs_kod, c.yos_kod, olay_tipi, kaynak_tipi)
    if s is None:
        return None
    t = now()
    e = Event(olay_no=str(uuid.uuid4()), olay_abonelik_no=s.olay_abonelik_no, hhs_kod=c.hhs_kod, yos_kod=c.yos_kod,
              olay_tipi=olay_tipi, kaynak_tipi=kaynak_tipi, kaynak_no=kaynak_no, olay_zamani=t,
              status="bekliyor", attempts=0, next_attempt_at=t)
    db.add(e)
    return e


def payload(e: Event) -> dict:
    return {
        "olayNo": e.olay_no,
        "olayAbonelikNo": e.olay_abonelik_no,
        "olayTipi": e.olay_tipi,
        "kaynakTipi": e.kaynak_tipi,
        "kaynakNo": e.kaynak_no,
        "olayZamani": aware(e.olay_zamani).astimezone(TR_TZ).isoformat(timespec="seconds"),
        "katilimciBlg": {"hhsKod": e.hhs_kod, "yosKod": e.yos_kod},
    }


# ------------------------------------------------------------------ tarama
def scan_balances(db: Session) -> int:
    """06 izni olan aktif rızalarda son taramadan bu yana yeni hareket varsa BAKIYE olayı üret."""
    from .generator import store
    from .generator.personas import PERSONAS

    t = now()
    count = 0
    for c in db.scalars(select(Consent).where(Consent.riza_drm == "K")):
        if "06" not in c.izin_tur or c.kmlk_vrs not in PERSONAS:
            continue
        if subscription_for(db, c.hhs_kod, c.yos_kod, "KAYNAK_GUNCELLENDI", "BAKIYE") is None:
            continue
        st = db.get(ScanState, c.riza_no)
        if st is None:
            db.add(ScanState(riza_no=c.riza_no, last_scan=t))
            continue
        since = aware(st.last_scan)
        for ref in c.secili_hesaplar:
            view = store.account_view(c.kmlk_vrs, ref, db)
            if any(since < tx["islGrckZaman"] <= t for tx in view.txs):
                emit(db, c, "KAYNAK_GUNCELLENDI", "BAKIYE", ref)
                count += 1
        st.last_scan = t
    db.commit()
    return count


def scan_timeouts(db: Session) -> None:
    """Bekleyen rızalarda süre aşımını uygula (ayrık GKD başarısız olayı için)."""
    from . import consent_service as svc
    for c in db.scalars(select(Consent).where(Consent.riza_drm.in_(("B", "Y", "K")))):
        svc.apply_timeouts(db, c)


# ------------------------------------------------------------------ iletim
async def _post(url: str, body: dict) -> int:
    async with httpx.AsyncClient(timeout=5) as client:
        r = await client.post(url, json=body, headers={"X-Request-ID": str(uuid.uuid4())})
        return r.status_code


sender = _post  # testlerde değiştirilebilir


def _retry_delay(e: Event):
    fast = e.kaynak_tipi == "KART_BILGILERI" or e.olay_tipi.startswith("AYRIK_GKD")
    return config.EVENT_RETRY["hizli" if fast else "standart"] * config.EVENT_RETRY_SCALE


async def deliver_due(db: Session) -> int:
    t = now()
    due = db.scalars(select(Event).where(Event.status == "bekliyor")).all()
    sent = 0
    for e in due:
        if aware(e.next_attempt_at) > t:
            continue
        e.attempts += 1
        try:
            code = await sender(config.YOS_EVENT_URL, payload(e))
            ok = code == 202
            e.last_error = None if ok else f"HTTP {code}"
        except Exception as ex:  # bağlantı hatası vb.
            ok = False
            e.last_error = str(ex)[:300]
        if ok:
            e.status = "iletildi"
            sent += 1
        elif e.attempts >= config.EVENT_MAX_ATTEMPTS:
            e.status = "iletilemedi"
        else:
            e.next_attempt_at = t + _retry_delay(e)
        db.commit()
    return sent


async def tick() -> None:
    db = SessionLocal()
    try:
        scan_timeouts(db)
        scan_balances(db)
        await deliver_due(db)
    finally:
        db.close()


async def loop() -> None:
    while True:
        try:
            await tick()
        except Exception:  # döngü asla ölmesin
            log.exception("olay döngüsü hatası")
        await asyncio.sleep(config.EVENT_TICK_SEC)
