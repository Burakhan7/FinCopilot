"""Sahte bankalar ve test kullanıcıları.

Banka adları kurgusaldır. Kullanıcı, hesap ve kart bilgileri
generator/personas.py'deki personalardan türetilir.
"""
from .generator.personas import PERSONAS

BANKS = {
    "9001": {"name": "Anadolu Bank", "color": "#0b5cab"},
    "9002": {"name": "Ege Bank", "color": "#0f8a5f"},
    "9003": {"name": "Marmara Bank", "color": "#b8401a"},
}

TEST_USERS = {t: {"name": p["name"], "password": p["password"]} for t, p in PERSONAS.items()}


def products_for(tckn: str, hhs_kod: str) -> dict:
    """GKD seçim ekranı için kullanıcının o bankadaki hesap ve kartları."""
    p = PERSONAS.get(tckn)
    if p is None:
        return {"accounts": [], "cards": []}
    from .generator.store import account_number
    accounts = [
        {"hspRef": a["ref"], "hspNo": account_number(a), "hspUrunAdi": a["product"], "prBrm": a["currency"]}
        for a in p["accounts"] if a["bank"] == hhs_kod
    ]
    cards = [
        {"kartRef": c["ref"], "kartSonNo": c["last4"], "kartUrunAdi": c["product"], "kartTipi": c["kartTipi"]}
        for c in p["cards"] if c["bank"] == hhs_kod
    ]
    return {"accounts": accounts, "cards": cards}
