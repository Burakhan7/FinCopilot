"""Rıza durum makinesi ve iş kuralları.

Durumlar (ÖHVPS): B Yetki Bekleniyor → Y Yetkilendirildi → K Yetki Kullanıldı
→ S Yetki Sonlandırıldı | I Yetki İptal (+ rizaIptDtyKod)
"""
import secrets
import uuid
from datetime import datetime, timezone

from sqlalchemy import select
from sqlalchemy.orm import Session

from . import config, errors
from .config import TR_TZ
from .db import AuthCode, Consent, Token
from .schemas import (
    IZIN_TURLERI,
    GkdYanit,
    HesapBilgisiRizasi,
    HesapBilgisiRizasiIstegi,
    HspBlgYanit,
    IznBlg,
    KatilimciBlg,
    KmlkYanit,
    RzBlg,
)

ACTIVE_STATES = ("B", "Y", "K")


def now() -> datetime:
    return datetime.now(timezone.utc)


def aware(dt: datetime | None) -> datetime | None:
    """SQLite saat dilimini saklamaz; naive değerleri UTC kabul et."""
    if dt is None:
        return None
    return dt if dt.tzinfo else dt.replace(tzinfo=timezone.utc)


def tr(dt: datetime | None) -> datetime | None:
    dt = aware(dt)
    return dt.astimezone(TR_TZ) if dt else None


# ---------- Doğrulama ----------
def validate_request(req: HesapBilgisiRizasiIstegi, hdr_hhs: str, hdr_yos: str) -> None:
    from .seed import BANKS

    if req.katilimciBlg.hhsKod not in BANKS or req.katilimciBlg.hhsKod != hdr_hhs:
        raise errors.invalid_aspsp()
    if req.katilimciBlg.yosKod not in config.ALLOWED_YOS_CODES or req.katilimciBlg.yosKod != hdr_yos:
        raise errors.invalid_tpp()

    izin = set(req.hspBlg.iznBlg.iznTur)
    if not izin <= IZIN_TURLERI or len(izin) != len(req.hspBlg.iznBlg.iznTur):
        raise errors.incorrect_permission("Geçersiz veya tekrarlanan izin türü.")
    if "01" not in izin and "07" not in izin:
        raise errors.incorrect_permission("01 (Temel Hesap) veya 07 (Temel Kart) izinlerinden en az biri zorunlu.")
    if "05" in izin and "04" not in izin:
        raise errors.incorrect_permission("05 (Ayrıntılı İşlem) için 04 (Temel İşlem) gerekli.")
    if "06" in izin and "03" not in izin:
        raise errors.incorrect_permission("06 (Anlık Bakiye Bildirimi) için 03 (Bakiye) gerekli.")
    # Mock varsayımı: hesap/kart alt izinleri ilgili temel izin olmadan anlamsız
    if ({"02", "03", "04", "05", "06"} & izin) and "01" not in izin:
        raise errors.incorrect_permission("Hesap izinleri için 01 (Temel Hesap) gerekli.")
    if ({"08", "09"} & izin) and "07" not in izin:
        raise errors.incorrect_permission("Kart izinleri için 07 (Temel Kart) gerekli.")

    t = now()
    son = aware(req.hspBlg.iznBlg.erisimIzniSonTrh)
    max_days = config.MAX_CONSENT_DAYS[req.kmlk.ohkTur]
    if son < t + config.MIN_CONSENT:
        raise errors.invalid_time("erisimIzniSonTrh en az 1 gün sonrası olmalı.")
    if (son - t).days > max_days:
        raise errors.invalid_time(f"erisimIzniSonTrh en fazla {max_days} gün sonrası olabilir.")

    bsl = aware(req.hspBlg.iznBlg.hesapIslemBslZmn)
    bts = aware(req.hspBlg.iznBlg.hesapIslemBtsZmn)
    if {"04", "05", "09"} & izin and (bsl is None or bts is None):
        raise errors.invalid_time("İşlem izinleri için hesapIslemBslZmn ve hesapIslemBtsZmn zorunlu.")
    if bsl and bts:
        if bsl >= bts:
            raise errors.invalid_time("hesapIslemBslZmn, hesapIslemBtsZmn'den önce olmalı.")
        if (t - bsl).days > config.MAX_TX_HISTORY_DAYS:
            raise errors.invalid_time("İşlem başlangıcı en fazla 12 ay geriye gidebilir.")
        if (bts - t).days > config.MAX_TX_HISTORY_DAYS:
            raise errors.invalid_time("İşlem bitişi en fazla 12 ay ileri olabilir.")

    if req.gkd.yetYntm == "A" and req.gkd.ayrikGkd is None:
        raise errors.invalid_field("Ayrık GKD (yetYntm=A) için ayrikGkd zorunlu.")
    if req.kmlk.kmlkTur == "K" and not (req.kmlk.kmlkVrs.isdigit() and len(req.kmlk.kmlkVrs) == 11):
        raise errors.invalid_field("kmlkTur=K için kmlkVrs 11 haneli TCKN olmalı.")


# ---------- Oluşturma / okuma ----------
def create_consent(db: Session, req: HesapBilgisiRizasiIstegi) -> Consent:
    t = now()
    c = Consent(
        riza_no=str(uuid.uuid4()),
        hhs_kod=req.katilimciBlg.hhsKod,
        yos_kod=req.katilimciBlg.yosKod,
        kmlk_tur=req.kmlk.kmlkTur,
        kmlk_vrs=req.kmlk.kmlkVrs,
        ohk_tur=req.kmlk.ohkTur,
        izin_tur=sorted(req.hspBlg.iznBlg.iznTur),
        erisim_izni_son_trh=aware(req.hspBlg.iznBlg.erisimIzniSonTrh),
        hesap_isl_bsl_zmn=aware(req.hspBlg.iznBlg.hesapIslemBslZmn),
        hesap_isl_bts_zmn=aware(req.hspBlg.iznBlg.hesapIslemBtsZmn),
        yet_yntm=req.gkd.yetYntm,
        yon_adr=req.gkd.yonAdr,
        ayrik_gkd=req.gkd.ayrikGkd.model_dump() if req.gkd.ayrikGkd else None,
        riza_drm="B",
        olus_zmn=t,
        gncl_zmn=t,
        gkd_son_zmn=t + config.GKD_TIMEOUT,
        secili_hesaplar=[],
        secili_kartlar=[],
    )
    # Aynı kullanıcı + HHS + YÖS için onay bekleyen eski istekler yeni talep ile iptal edilir (kod 01).
    # Böylece banka ekranında aynı istek iki kez görünmez.
    stale = db.scalars(select(Consent).where(
        Consent.kmlk_vrs == c.kmlk_vrs, Consent.hhs_kod == c.hhs_kod, Consent.yos_kod == c.yos_kod,
        Consent.riza_drm == "B")).all()
    for s in stale:
        s.riza_drm, s.riza_ipt_dty_kod, s.gncl_zmn = "I", "01", t
        _notify(db, s)
    db.add(c)
    db.commit()
    return c


def get_consent(db: Session, riza_no: str) -> Consent:
    c = db.get(Consent, riza_no)
    if c is None:
        raise errors.consent_not_found()
    apply_timeouts(db, c)
    return c


def apply_timeouts(db: Session, c: Consent) -> None:
    """Süre aşımlarını okuma anında uygula (tembel değerlendirme)."""
    t = now()
    changed = False
    if c.riza_drm == "B" and t > aware(c.gkd_son_zmn):
        c.riza_drm, c.riza_ipt_dty_kod, changed = "I", "04", True
    elif c.riza_drm == "Y" and c.yet_tmm_zmn and t > aware(c.yet_tmm_zmn) + config.YETKOD_TIMEOUT:
        c.riza_drm, c.riza_ipt_dty_kod, changed = "I", "05", True
    elif c.riza_drm == "K" and t > aware(c.erisim_izni_son_trh):
        c.riza_drm, changed = "S", True
    if changed:
        c.gncl_zmn = t
        _notify(db, c)
        db.commit()


def cancel(db: Session, c: Consent, kod: str) -> None:
    c.riza_drm = "I"
    c.riza_ipt_dty_kod = kod
    c.gncl_zmn = now()
    _notify(db, c)
    db.commit()


def _notify(db: Session, c: Consent) -> None:
    """Rıza durum değişikliğinde YÖS'e olay (abonelik varsa).

    - Ayrık GKD sırasında iptal/süre aşımı → AYRIK_GKD_BASARISIZ
    - YÖS dışından gelen değişiklikler (01, 02, 04, 05, S) → KAYNAK_GUNCELLENDI / HESAP_BILGISI_RIZASI
    - YÖS'ün kendi iptali (03) için olay üretilmez.
    """
    from . import events
    if c.yet_yntm == "A" and c.riza_drm == "I" and c.riza_ipt_dty_kod in ("04", "07", "08", "09", "10", "11", "12", "13", "14", "99"):
        events.emit(db, c, "AYRIK_GKD_BASARISIZ", "HESAP_BILGISI_RIZASI", c.riza_no)
    elif c.riza_drm == "S" or c.riza_ipt_dty_kod in ("01", "02", "04", "05"):
        events.emit(db, c, "KAYNAK_GUNCELLENDI", "HESAP_BILGISI_RIZASI", c.riza_no)


def validate_ayrik(db: Session, req: HesapBilgisiRizasiIstegi) -> None:
    """Ayrık GKD ön koşulu: YÖS, AYRIK_GKD_BASARILI ve AYRIK_GKD_BASARISIZ olaylarına abone olmalı."""
    if req.gkd.yetYntm != "A":
        return
    from . import events
    for olay in ("AYRIK_GKD_BASARILI", "AYRIK_GKD_BASARISIZ"):
        if events.subscription_for(db, req.katilimciBlg.hhsKod, req.katilimciBlg.yosKod, olay,
                                   "HESAP_BILGISI_RIZASI") is None:
            raise errors.invalid_field(f"Ayrık GKD için {olay} olayına abonelik gerekli (/olay-abonelik).")


def pending_auth_code(db: Session, c: Consent) -> str | None:
    if c.riza_drm != "Y":
        return None
    code = db.scalars(select(AuthCode).where(AuthCode.riza_no == c.riza_no, AuthCode.used.is_(False))).first()
    return code.yet_kod if code else None


def to_response(c: Consent, yet_kod: str | None = None) -> HesapBilgisiRizasi:
    hhs_yon_adr = f"{config.PUBLIC_BASE_URL}/gkd/{c.riza_no}" if c.yet_yntm == "Y" else None
    return HesapBilgisiRizasi(
        rzBlg=RzBlg(
            rizaNo=c.riza_no,
            olusZmn=tr(c.olus_zmn),
            gnclZmn=tr(c.gncl_zmn),
            rizaDrm=c.riza_drm,
            rizaIptDtyKod=c.riza_ipt_dty_kod,
        ),
        kmlk=KmlkYanit(kmlkTur=c.kmlk_tur, kmlkVrs=c.kmlk_vrs, ohkTur=c.ohk_tur),
        katilimciBlg=KatilimciBlg(hhsKod=c.hhs_kod, yosKod=c.yos_kod),
        gkd=GkdYanit(
            yetYntm=c.yet_yntm,
            yonAdr=c.yon_adr,
            hhsYonAdr=hhs_yon_adr,
            yetTmmZmn=tr(c.yet_tmm_zmn),
            ayrikGkd=c.ayrik_gkd,
            yetKod=yet_kod if c.yet_yntm == "A" else None,
        ),
        hspBlg=HspBlgYanit(
            iznBlg=IznBlg(
                iznTur=c.izin_tur,
                erisimIzniSonTrh=tr(c.erisim_izni_son_trh),
                hesapIslemBslZmn=tr(c.hesap_isl_bsl_zmn),
                hesapIslemBtsZmn=tr(c.hesap_isl_bts_zmn),
            )
        ),
    )


# ---------- GKD sonucu ----------
def authorize(db: Session, c: Consent, hesaplar: list[str], kartlar: list[str]) -> str:
    """Kullanıcı bankada onayladı: B → Y, yetKod üret, eski aktif rızayı iptal et (01)."""
    t = now()
    previous = db.scalars(
        select(Consent).where(
            Consent.kmlk_vrs == c.kmlk_vrs,
            Consent.hhs_kod == c.hhs_kod,
            Consent.yos_kod == c.yos_kod,
            Consent.riza_no != c.riza_no,
            Consent.riza_drm.in_(ACTIVE_STATES),
        )
    ).all()
    for p in previous:
        p.riza_drm, p.riza_ipt_dty_kod, p.gncl_zmn = "I", "01", t
        _notify(db, p)

    c.riza_drm = "Y"
    c.yet_tmm_zmn = t
    c.gncl_zmn = t
    c.secili_hesaplar = hesaplar
    c.secili_kartlar = kartlar
    code = AuthCode(yet_kod=secrets.token_urlsafe(24), riza_no=c.riza_no, expires_at=t + config.YETKOD_TIMEOUT)
    db.add(code)
    if c.yet_yntm == "A":
        from . import events
        events.emit(db, c, "AYRIK_GKD_BASARILI", "HESAP_BILGISI_RIZASI", c.riza_no)
    db.commit()
    return code.yet_kod


# ---------- Token ----------
def issue_token_from_code(db: Session, riza_no: str, yet_kod: str) -> Token:
    c = get_consent(db, riza_no)
    code = db.get(AuthCode, yet_kod)
    if code is None or code.riza_no != riza_no or code.used or now() > aware(code.expires_at):
        raise errors.unauthorized("yetKod geçersiz, kullanılmış veya süresi dolmuş.")
    if c.riza_drm != "Y":
        raise errors.consent_revoked()
    code.used = True
    c.riza_drm = "K"
    c.gncl_zmn = now()
    tok = _new_token(c)
    db.add(tok)
    db.commit()
    return tok


def issue_token_from_refresh(db: Session, riza_no: str, refresh: str) -> Token:
    c = get_consent(db, riza_no)
    if c.riza_drm != "K":
        raise errors.consent_revoked()
    old = db.scalars(select(Token).where(Token.refresh_token == refresh, Token.riza_no == riza_no)).first()
    if old is None or now() > aware(old.refresh_expires_at):
        raise errors.unauthorized("yenilemeBelirteci geçersiz veya süresi dolmuş.")
    tok = _new_token(c, refresh_token=old.refresh_token)  # ÖHVPS: yenileme belirteci değişmez
    db.add(tok)
    db.commit()
    return tok


def _new_token(c: Consent, refresh_token: str | None = None) -> Token:
    t = now()
    return Token(
        access_token=secrets.token_urlsafe(32),
        refresh_token=refresh_token or secrets.token_urlsafe(32),
        riza_no=c.riza_no,
        access_expires_at=min(t + config.ACCESS_TOKEN_TTL, aware(c.erisim_izni_son_trh)),
        refresh_expires_at=aware(c.erisim_izni_son_trh),
    )


def require_access_token(db: Session, access_token: str | None, riza_no: str | None = None) -> Consent:
    """Veri uçları ve rıza iptali için X-Access-Token kontrolü."""
    if not access_token:
        raise errors.unauthorized("X-Access-Token başlığı eksik.")
    tok = db.get(Token, access_token)
    if tok is None or now() > aware(tok.access_expires_at):
        raise errors.unauthorized()
    if riza_no and tok.riza_no != riza_no:
        raise errors.unauthorized("Belirteç bu rızaya ait değil.")
    c = get_consent(db, tok.riza_no)
    if c.riza_drm != "K":
        raise errors.consent_revoked()
    return c
