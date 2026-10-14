"""Üretilen veriyi önbellekler ve ÖHVPS alan adlarına çevirir.

Veri her gün için bir kez üretilir (bitiş günü = bugün, son 365 gün).
Alan adları tek yerde (bu dosya) olduğu için spesifikasyonla farklılık
bulunursa sadece burası değişir.
"""
from __future__ import annotations

import hashlib
from datetime import date, datetime
from decimal import Decimal as D
from functools import lru_cache

from ..config import TR_TZ
from .engine import Simulator
from .personas import PERSONAS


def today_tr() -> date:
    return datetime.now(TR_TZ).date()


@lru_cache(maxsize=16)
def simulate(tckn: str, end: date) -> Simulator:
    return Simulator(tckn, PERSONAS[tckn], end).run()


def dataset(tckn: str) -> Simulator:
    return simulate(tckn, today_tr())


def amt(x: D) -> str:
    return f"{x:.2f}"


def iso(dt: datetime | date) -> str:
    if isinstance(dt, datetime):
        return dt.astimezone(TR_TZ).isoformat(timespec="seconds")
    return datetime(dt.year, dt.month, dt.day, tzinfo=TR_TZ).isoformat(timespec="seconds")


# ---------------------------------------------------------------- IBAN
def _iban(bank_code: str, account_no: str) -> str:
    """Geçerli kontrol basamaklı kurgusal TR IBAN üretir (TRkk + 5 banka + 1 rezerv + 16 hesap)."""
    bban = f"{int(bank_code):05d}0{account_no:0>16}"
    numeric = "".join(str(int(ch, 36)) for ch in bban + "TR00")
    check = 98 - int(numeric) % 97
    return f"TR{check:02d}{bban}"


def account_number(acc_meta: dict) -> str:
    digits = str(int(hashlib.sha256(acc_meta["ref"].encode()).hexdigest(), 16))[:16]
    return _iban(acc_meta["bank"], digits)


# ---------------------------------------------------------------- dönüştürücüler
def map_account(acc_meta: dict, persona: dict, riza_no: str, detailed: bool) -> dict:
    out = {
        "rizaNo": riza_no,
        "hspTml": {
            "hspRef": acc_meta["ref"],
            "hspNo": account_number(acc_meta),
            "hspShb": persona["name"].upper(),
            "subeAdi": acc_meta["sube"],
            "kisaAd": acc_meta["product"],
            "prBrm": acc_meta["currency"],
            "hspTur": "B",
            "hspTip": acc_meta["hspTip"],
            "hspUrunAdi": acc_meta["product"],
            "hspDrm": "AKTIF",
        },
    }
    if detailed:
        out["hspDty"] = {"hspAclsTrh": iso(date.fromisoformat(acc_meta["opened"]))}
    return out


def map_balance(acc_state, now: datetime) -> dict:
    bal = acc_state.balance_at(now)
    out = {
        "hspRef": acc_state.meta["ref"],
        "bky": {"bkyTtr": amt(bal), "blkTtr": "0.00", "prBrm": acc_state.meta["currency"], "bkyZmn": iso(now)},
    }
    if acc_state.kmh_limit > 0:
        out["krdHsp"] = {"kulKrdTtr": amt(max(D("0"), -bal)), "krdDhlGstr": "0"}
    return out


def map_tx(tx: dict, detailed: bool) -> dict:
    tml = {
        "islNo": tx["islNo"], "refNo": tx["refNo"], "islTtr": amt(tx["islTtr"]), "prBrm": tx["prBrm"],
        "islGrckZaman": iso(tx["islGrckZaman"]), "kanal": tx["kanal"], "brcAlc": tx["brcAlc"], "islTur": tx["islTur"],
    }
    out = {"islTml": tml}
    if detailed:
        if tx["islAmc"]:
            tml["islAmc"] = tx["islAmc"]
        tml["islAcklm"] = tx["islAcklm"]
        if tx["ohkMsj"]:
            tml["ohkMsj"] = tx["ohkMsj"]
        dty = {}
        if tx["karsiUnvan"]:
            dty["odmKsnm"] = tx["karsiUnvan"]
        if tx["karsiIban"]:
            dty["odmHsbNo"] = tx["karsiIban"]
        if dty:
            out["islDty"] = dty
    return out


def map_card(card_meta: dict, persona: dict, riza_no: str) -> dict:
    return {
        "rizaNo": riza_no,
        "kartRef": card_meta["ref"],
        "kartSonNo": card_meta["last4"],
        "kartSh": persona["name"].upper(),
        "kartTipi": card_meta["kartTipi"],
        "altKartTipi": "A",
        "kartFormu": "F",
        "kartTuru": "B",
        "kartStatu": "A",
        "kartSema": card_meta["kartSema"],
        "kartUrunAdi": card_meta["product"],
    }


def map_card_detail(card_state, now: datetime) -> dict:
    sts = [s for s in card_state.statements if s["hesapKesimTarihi"] <= now.date()]
    st = sts[-1] if sts else None
    return {
        "kartRef": card_state.meta["ref"],
        "hesapKesimTarihi": iso(st["hesapKesimTarihi"]) if st else None,
        "sonOdemeTarihi": iso(st["sonOdemeTarihi"]) if st else None,
        "donemBorcu": amt(st["donemBorcu"]) if st else "0.00",
        "asgariOdeme": amt(st["asgariOdeme"]) if st else "0.00",
        "limitTutari": amt(card_state.limit),
        "kullanilanLimit": amt(card_state.used),
        "prBrm": "TRY",
    }


def map_card_tx(mv: dict) -> dict:
    out = {
        "islemNo": mv["islemNo"], "islemTarihi": iso(mv["islemTarihi"]), "islemTutari": amt(mv["islemTutari"]),
        "islemParaBirimi": mv["islemParaBirimi"], "brcAlc": mv["borcAlacak"], "islemTuru": mv["islemTuru"],
        "islemAciklama": mv["islemAciklama"], "referansNo": mv["referansNo"],
    }
    if mv["uyeIsyeri"]:
        out["uyeIsyeri"] = mv["uyeIsyeri"]
    if mv["taksitSayisi"]:
        out["taksitSayisi"] = mv["taksitSayisi"]
    if mv["dovizBilgisi"]:
        fx = mv["dovizBilgisi"]
        out["dovizBilgisi"] = {"orijinalTutar": amt(fx["orijinalTutar"]), "orijinalParaBirimi": fx["orijinalParaBirimi"],
                               "kur": f"{fx['kur']:.4f}"}
    return out
