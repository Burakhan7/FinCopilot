"""Sahte bankalar ve test kullanıcıları.

Banka adları kurgusaldır. Faz 1'in ikinci adımında hesap/kart verisi
sentetik veri üreticisinden gelecek; burada GKD seçim ekranı için
asgari hesap ve kart listesi var.
"""

BANKS = {
    "9001": {"name": "Anadolu Bank", "color": "#0b5cab"},
    "9002": {"name": "Ege Bank", "color": "#0f8a5f"},
    "9003": {"name": "Marmara Bank", "color": "#b8401a"},
}

# TCKN'ler algoritmaya uygun ama kurgusal numaralardır.
TEST_USERS = {
    "10000000146": {"name": "Ayşe Yılmaz", "password": "Test1234"},
    "10000000214": {"name": "Mehmet Kaya", "password": "Test1234"},
    "10000000382": {"name": "Zeynep Demir", "password": "Test1234"},
}

# (tckn, hhsKod) -> ürünler. hspRef / kartRef bankanın verdiği referanslardır.
PRODUCTS = {
    ("10000000146", "9001"): {
        "accounts": [
            {"hspRef": "A9001-AY-01", "hspNo": "TR120090010000000000000146", "hspUrunAdi": "Vadesiz TL", "prBrm": "TRY"},
        ],
        "cards": [
            {"kartRef": "K9001-AY-01", "kartSonNo": "4412", "kartUrunAdi": "Genç Kart", "kartTipi": "K"},
        ],
    },
    ("10000000146", "9002"): {
        "accounts": [
            {"hspRef": "A9002-AY-01", "hspNo": "TR340090020000000000000146", "hspUrunAdi": "Maaş Hesabı", "prBrm": "TRY"},
        ],
        "cards": [],
    },
    ("10000000214", "9001"): {
        "accounts": [
            {"hspRef": "A9001-MK-01", "hspNo": "TR560090010000000000000214", "hspUrunAdi": "Maaş Hesabı", "prBrm": "TRY"},
            {"hspRef": "A9001-MK-02", "hspNo": "TR780090010000000000001214", "hspUrunAdi": "Döviz Hesabı", "prBrm": "USD"},
        ],
        "cards": [
            {"kartRef": "K9001-MK-01", "kartSonNo": "7731", "kartUrunAdi": "Platinum Kart", "kartTipi": "K"},
        ],
    },
    ("10000000214", "9003"): {
        "accounts": [
            {"hspRef": "A9003-MK-01", "hspNo": "TR900090030000000000000214", "hspUrunAdi": "Vadesiz TL", "prBrm": "TRY"},
        ],
        "cards": [
            {"kartRef": "K9003-MK-01", "kartSonNo": "1029", "kartUrunAdi": "Bonus Kart", "kartTipi": "K"},
            {"kartRef": "K9003-MK-02", "kartSonNo": "5580", "kartUrunAdi": "Banka Kartı", "kartTipi": "B"},
        ],
    },
    ("10000000382", "9002"): {
        "accounts": [
            {"hspRef": "A9002-ZD-01", "hspNo": "TR230090020000000000000382", "hspUrunAdi": "Vadesiz TL", "prBrm": "TRY"},
        ],
        "cards": [
            {"kartRef": "K9002-ZD-01", "kartSonNo": "3307", "kartUrunAdi": "Gold Kart", "kartTipi": "K"},
        ],
    },
    ("10000000382", "9003"): {
        "accounts": [
            {"hspRef": "A9003-ZD-01", "hspNo": "TR450090030000000000000382", "hspUrunAdi": "Esnek Hesap", "prBrm": "TRY"},
        ],
        "cards": [
            {"kartRef": "K9003-ZD-01", "kartSonNo": "8814", "kartUrunAdi": "Bonus Kart", "kartTipi": "K"},
        ],
    },
}


def products_for(tckn: str, hhs_kod: str) -> dict:
    return PRODUCTS.get((tckn, hhs_kod), {"accounts": [], "cards": []})
