"""Test personaları.

Tutarlar 2025 Ekim fiyatlarıyla verilir; motor aylık fiyat artışı uygular (MONTHLY_PRICE_DRIFT).
`month` alanları simülasyonun başından itibaren ay indeksidir (0 = ilk ay).
"""
from decimal import Decimal as D

MONTHLY_PRICE_DRIFT = D("0.022")  # temsili aylık fiyat artışı

# Temsili kur patikası (dönem başı → dönem sonu, doğrusal)
FX_PATH = {"USD": (D("41.80"), D("46.50")), "EUR": (D("48.60"), D("54.10"))}

PERSONAS = {
    # ------------------------------------------------------------------ Ayşe
    "10000000146": {
        "name": "Ayşe Yılmaz", "password": "Test1234", "birth_date": "2004-03-12",
        "phone": "+90532*****46", "city": "Manisa", "job": "Üniversite öğrencisi (yarı zamanlı kafe çalışanı)",
        "declared_monthly_income": D("14000"), "card_years": 2,
        "accounts": [
            {"bank": "9001", "ref": "A9001-AY-01", "product": "Vadesiz TL", "hspTip": "VADESIZ",
             "currency": "TRY", "opened": "2022-09-15", "initial": D("2400"), "kmh_limit": D("0"), "sube": "YUNUSEMRE/MANISA"},
            {"bank": "9002", "ref": "A9002-AY-01", "product": "Harçlık Hesabı", "hspTip": "VADESIZ",
             "currency": "TRY", "opened": "2023-02-01", "initial": D("1100"), "kmh_limit": D("0"), "sube": "SEHZADELER/MANISA"},
        ],
        "cards": [
            {"bank": "9001", "ref": "K9001-AY-01", "product": "Genç Kart", "kartTipi": "K", "kartSema": "T",
             "last4": "4412", "limit": D("25000"), "statement_day": 8, "pay_from": "A9001-AY-01",
             "annual_fee": D("0"), "annual_fee_month": None},
        ],
        "incomes": [
            {"account": "A9001-AY-01", "amount": D("9000"), "day": 1, "jitter": 1, "islTur": "EFT", "islAmc": "05",
             "from": "LEZZET KAFE GIDA LTD STI", "desc": "MAAS ODEMESI", "raise": {"month": 3, "amount": D("10500")}},
            {"account": "A9002-AY-01", "amount": D("4000"), "day": 5, "jitter": 6, "islTur": "FAST", "islAmc": "07",
             "from": "HATICE YILMAZ", "desc": "harclik", "vary": D("0.25"),
             "raise": {"month": 3, "amount": D("5000")}},
            {"account": "A9002-AY-01", "amount": D("3000"), "day": 10, "jitter": 2, "islTur": "HAVALE", "islAmc": "10",
             "from": "KREDI VE YURTLAR GENEL MUD", "desc": "OGRENIM BURSU", "months_active": list(range(0, 9)) + [11, 12]},
        ],
        "fixed": [
            {"account": "A9001-AY-01", "amount": D("6500"), "day": 3, "islTur": "EFT", "islAmc": "03",
             "to": "OZEL GUNES KIZ OGRENCI YURDU", "desc": "yurt ucreti", "drift": True},
            {"account": "A9002-AY-01", "amount": D("0"), "day": 12, "islTur": "FAST", "islAmc": None,
             "to": "AYSE YILMAZ", "desc": "HESAPLARIM ARASI", "own_transfer_to": "A9001-AY-01", "sweep_keep": D("1500")},
        ],
        "subscriptions": [
            {"card": "K9001-AY-01", "merchant": "SPOTIFY P2F8A1 STOCKHOLM", "amount": D("49.99"), "day": 14},
            {"card": "K9001-AY-01", "merchant": "NETFLIX.COM", "amount": D("189.99"), "day": 21,
             "price_change": {"month": 6, "amount": D("229.99")}},
            {"card": "K9001-AY-01", "merchant": "GOOGLE *YOUTUBEPREMIUM", "amount": D("57.99"), "day": 9},
            {"card": "K9001-AY-01", "merchant": "PLAYSTATION NETWORK", "amount": D("299.00"), "day": 27},
        ],
        "spending": {  # kategori: (aylık işlem adedi, ortalama tutar çarpanı, internet payı, {kart/hesap: ağırlık})
            "yeme_icme": (12, D("0.50"), 0.45, {"K9001-AY-01": 1}),
            "market": (6, D("0.50"), 0.15, {"K9001-AY-01": 1}),
            "ulasim": (10, D("0.30"), 0.10, {"K9001-AY-01": 1}),
            "giyim": (1, D("0.70"), 0.60, {"K9001-AY-01": 1}),
            "saglik_kozmetik": (2, D("0.70"), 0.20, {"K9001-AY-01": 1}),
            "eglence": (1.5, D("0.55"), 0.40, {"K9001-AY-01": 1}),
        },
        "events": [
            {"type": "installment", "card": "K9001-AY-01", "month": 4, "day": 18, "merchant": "YENILENMIS TELEFON*EASYCEP",
             "total": D("13500"), "n": 6},
            {"type": "late_payment", "card": "K9001-AY-01", "month": 7, "days_late": 3},
            {"type": "min_payment_only", "card": "K9001-AY-01", "month": 9},
        ],
    },
    # ------------------------------------------------------------------ Mehmet
    "10000000214": {
        "name": "Mehmet Kaya", "password": "Test1234", "birth_date": "1992-07-04",
        "phone": "+90533*****14", "city": "Istanbul", "job": "Yazılım geliştirici",
        "declared_monthly_income": D("85000"), "card_years": 8,
        "accounts": [
            {"bank": "9001", "ref": "A9001-MK-01", "product": "Maaş Hesabı", "hspTip": "VADESIZ",
             "currency": "TRY", "opened": "2019-05-02", "initial": D("38000"), "kmh_limit": D("0"), "sube": "ATASEHIR/ISTANBUL"},
            {"bank": "9001", "ref": "A9001-MK-02", "product": "Döviz Hesabı", "hspTip": "VADESIZ",
             "currency": "USD", "opened": "2021-11-20", "initial": D("1850"), "kmh_limit": D("0"), "sube": "ATASEHIR/ISTANBUL"},
            {"bank": "9003", "ref": "A9003-MK-01", "product": "Vadesiz TL", "hspTip": "KREDILI_MEVDUAT_HESABI",
             "currency": "TRY", "opened": "2020-03-10", "initial": D("3000"), "kmh_limit": D("20000"), "sube": "KADIKOY/ISTANBUL"},
        ],
        "cards": [
            {"bank": "9001", "ref": "K9001-MK-01", "product": "Platinum Kart", "kartTipi": "K", "kartSema": "M",
             "last4": "7731", "limit": D("120000"), "statement_day": 5, "pay_from": "A9001-MK-01",
             "annual_fee": D("1250"), "annual_fee_month": 8},
            {"bank": "9003", "ref": "K9003-MK-01", "product": "Bonus Kart", "kartTipi": "K", "kartSema": "V",
             "last4": "1029", "limit": D("80000"), "statement_day": 20, "pay_from": "A9003-MK-01",
             "annual_fee": D("0"), "annual_fee_month": None},
            {"bank": "9003", "ref": "K9003-MK-02", "product": "Banka Kartı", "kartTipi": "B", "kartSema": "T",
             "last4": "5580", "linked_account": "A9003-MK-01"},
        ],
        "incomes": [
            {"account": "A9001-MK-01", "amount": D("85000"), "day": 15, "jitter": 0, "islTur": "EFT", "islAmc": "05",
             "from": "DORUK YAZILIM TEKNOLOJI AS", "desc": "MAAS ODEMESI", "raise": {"month": 3, "amount": D("101000")}},
            {"account": "A9001-MK-01", "amount": D("60000"), "day": 15, "jitter": 0, "islTur": "EFT", "islAmc": "05",
             "from": "DORUK YAZILIM TEKNOLOJI AS", "desc": "PRIM ODEMESI", "months_active": [5]},
        ],
        "fixed": [
            {"account": "A9001-MK-01", "amount": D("32000"), "day": 1, "islTur": "FAST", "islAmc": "01",
             "to": "AHMET YILMAZ", "desc": "kira", "iban": "TR33000620000000000000001234", "raise": {"month": 9, "amount": D("41600")}},
            {"account": "A9001-MK-01", "amount": D("1500"), "day": 5, "islTur": "EFT", "islAmc": "11",
             "to": "MODA KONUTLARI SITE YONETIMI", "desc": "aidat", "fee": D("8.50"), "drift": True},
            {"account": "A9001-MK-01", "amount": D("1100"), "day": 12, "islTur": "KURUM_FATURA_ODEMESI", "islAmc": None,
             "to": "CK BOGAZICI ELEKTRIK", "desc": "ELEKTRIK FATURASI", "seasonal": "electric", "drift": True},
            {"account": "A9001-MK-01", "amount": D("1400"), "day": 18, "islTur": "KURUM_FATURA_ODEMESI", "islAmc": None,
             "to": "IGDAS", "desc": "DOGALGAZ FATURASI", "seasonal": "gas", "drift": True},
            {"account": "A9001-MK-01", "amount": D("380"), "day": 22, "islTur": "KURUM_FATURA_ODEMESI", "islAmc": None,
             "to": "ISKI", "desc": "SU FATURASI", "drift": True},
            {"account": "A9001-MK-01", "amount": D("20000"), "day": 16, "islTur": "FAST", "islAmc": None,
             "to": "MEHMET KAYA", "desc": "HESAPLARIM ARASI", "own_transfer_to": "A9003-MK-01", "cover_negative": True},
        ],
        "subscriptions": [
            {"card": "K9001-MK-01", "merchant": "TURKCELL SUPERONLINE FATURA", "amount": D("649.00"), "day": 7, "drift": True},
            {"card": "K9001-MK-01", "merchant": "VODAFONE FATURA ODEME", "amount": D("575.00"), "day": 11, "drift": True},
            {"card": "K9003-MK-01", "merchant": "NETFLIX.COM", "amount": D("289.99"), "day": 3,
             "price_change": {"month": 6, "amount": D("349.99")}},
            {"card": "K9003-MK-01", "merchant": "SPOTIFY P3K9Q2 STOCKHOLM", "amount": D("149.99"), "day": 17},
            {"card": "K9001-MK-01", "merchant": "APPLE.COM/BILL ICLOUD", "amount": D("119.99"), "day": 24},
            {"card": "K9003-MK-01", "merchant": "MACFIT SPOR {ilce}", "amount": D("2450.00"), "day": 1},
        ],
        "spending": {
            "market": (10, D("1.6"), 0.20, {"K9001-MK-01": 2, "K9003-MK-01": 2, "K9003-MK-02": 1}),
            "yeme_icme": (12, D("1.4"), 0.35, {"K9001-MK-01": 1, "K9003-MK-01": 2}),
            "akaryakit": (4, D("1.1"), 0.0, {"K9001-MK-01": 1, "K9003-MK-01": 1}),
            "ulasim": (5, D("0.9"), 0.30, {"K9001-MK-01": 1}),
            "giyim": (2, D("1.2"), 0.55, {"K9003-MK-01": 1}),
            "ev_yasam": (1, D("1.3"), 0.40, {"K9003-MK-01": 1}),
            "eglence": (2, D("1.0"), 0.40, {"K9001-MK-01": 1}),
            "saglik_kozmetik": (2, D("1.0"), 0.25, {"K9003-MK-02": 1, "K9001-MK-01": 1}),
            "seyahat": (0.35, D("1.2"), 0.80, {"K9001-MK-01": 1}),
        },
        "events": [
            {"type": "installment", "card": "K9003-MK-01", "month": 2, "day": 9, "merchant": "MEDIAMARKT ATASEHIR",
             "total": D("36000"), "n": 12},
            {"type": "fee", "account": "A9003-MK-01", "every_months": 3, "day": 28, "amount": D("150"),
             "desc": "HESAP ISLETIM UCRETI"},
        ],
    },
    # ------------------------------------------------------------------ Zeynep
    "10000000382": {
        "name": "Zeynep Demir", "password": "Test1234", "birth_date": "1997-11-23",
        "phone": "+90535*****82", "city": "Izmir", "job": "Serbest grafik tasarımcı",
        "declared_monthly_income": D("45000"), "card_years": 5,
        "accounts": [
            {"bank": "9002", "ref": "A9002-ZD-01", "product": "Vadesiz TL", "hspTip": "VADESIZ",
             "currency": "TRY", "opened": "2018-06-11", "initial": D("62000"), "kmh_limit": D("0"), "sube": "ALSANCAK/IZMIR"},
            {"bank": "9003", "ref": "A9003-ZD-01", "product": "Esnek Hesap", "hspTip": "KREDILI_MEVDUAT_HESABI",
             "currency": "TRY", "opened": "2021-01-19", "initial": D("4500"), "kmh_limit": D("10000"), "sube": "BORNOVA/IZMIR"},
        ],
        "cards": [
            {"bank": "9002", "ref": "K9002-ZD-01", "product": "Gold Kart", "kartTipi": "K", "kartSema": "M",
             "last4": "3307", "limit": D("60000"), "statement_day": 10, "pay_from": "A9002-ZD-01",
             "annual_fee": D("950"), "annual_fee_month": 4},
            {"bank": "9003", "ref": "K9003-ZD-01", "product": "Bonus Kart", "kartTipi": "K", "kartSema": "V",
             "last4": "8814", "limit": D("50000"), "statement_day": 25, "pay_from": "A9003-ZD-01",
             "annual_fee": D("0"), "annual_fee_month": None},
        ],
        "incomes": [
            {"account": "A9002-ZD-01", "irregular": True, "per_month": (1, 3), "range": (D("12000"), D("45000")),
             "islTur": "EFT", "islAmc": "06",
             "from_pool": ["ATLAS REKLAM AJANSI LTD STI", "MAVI DUZ TASARIM AS", "KUZEY E-TICARET LTD STI",
                           "PERA YAYINCILIK AS", "EGE GIDA PAZARLAMA LTD STI"], "desc": "fatura {no}"},
        ],
        "fixed": [
            {"account": "A9002-ZD-01", "amount": D("22000"), "day": 5, "islTur": "FAST", "islAmc": None,
             "to": "SELIN AKSOY", "desc": "kira", "iban": "TR780004600000000000005678"},
            {"account": "A9002-ZD-01", "amount": D("9500"), "day": 26, "islTur": "SGK_ODEMESI", "islAmc": None,
             "to": "SOSYAL GUVENLIK KURUMU", "desc": "BAG-KUR PRIM ODEMESI", "drift": True},
            {"account": "A9002-ZD-01", "amount": D("16000"), "day": 17, "islTur": "VERGI_ODEMESI", "islAmc": "16",
             "to": "GELIR IDARESI BASKANLIGI", "desc": "GECICI VERGI", "months_active": [1, 4, 7, 10], "vary": D("0.3")},
            {"account": "A9002-ZD-01", "amount": D("850"), "day": 14, "islTur": "KURUM_FATURA_ODEMESI", "islAmc": None,
             "to": "GEDIZ ELEKTRIK", "desc": "ELEKTRIK FATURASI", "seasonal": "electric", "drift": True},
            {"account": "A9002-ZD-01", "amount": D("11000"), "day": 2, "islTur": "FAST", "islAmc": None,
             "to": "ZEYNEP DEMIR", "desc": "HESAPLARIM ARASI", "own_transfer_to": "A9003-ZD-01", "vary": D("0.2"),
             "cover_negative": True},
        ],
        "subscriptions": [
            {"card": "K9002-ZD-01", "merchant": "ADOBE *CREATIVE CLOUD", "amount": D("22.99"), "currency": "USD", "day": 6,
             "price_change": {"month": 7, "amount": D("27.99")}},
            {"card": "K9002-ZD-01", "merchant": "FIGMA MONTHLY", "amount": D("16.00"), "currency": "USD", "day": 13},
            {"card": "K9002-ZD-01", "merchant": "OPENAI *CHATGPT SUBSCR", "amount": D("20.00"), "currency": "USD", "day": 19},
            {"card": "K9003-ZD-01", "merchant": "NETFLIX.COM", "amount": D("229.99"), "day": 8},
            {"card": "K9002-ZD-01", "merchant": "TURK TELEKOM FATURA", "amount": D("720.00"), "day": 20, "drift": True},
        ],
        "spending": {
            "market": (8, D("1.0"), 0.25, {"K9002-ZD-01": 1, "K9003-ZD-01": 1}),
            "yeme_icme": (18, D("0.9"), 0.30, {"K9002-ZD-01": 2, "K9003-ZD-01": 1}),
            "ulasim": (8, D("0.6"), 0.20, {"K9003-ZD-01": 1}),
            "giyim": (1.5, D("1.0"), 0.60, {"K9003-ZD-01": 1}),
            "elektronik": (0.5, D("1.0"), 0.70, {"K9002-ZD-01": 1}),
            "eglence": (2, D("0.9"), 0.35, {"K9003-ZD-01": 1}),
            "saglik_kozmetik": (2, D("0.9"), 0.25, {"K9002-ZD-01": 1}),
            "seyahat": (0.4, D("1.0"), 0.85, {"K9002-ZD-01": 1}),
        },
        "events": [
            {"type": "installment", "card": "K9002-ZD-01", "month": 3, "day": 22, "merchant": "VATAN BILGISAYAR KONAK",
             "total": D("39000"), "n": 9},
            {"type": "foreign_trip", "card": "K9002-ZD-01", "month": 6, "day": 10, "days": 5, "currency": "EUR",
             "merchants": ["ALBERT HEIJN AMSTERDAM", "GVB TICKETS AMSTERDAM", "CAFE DE JAREN AMSTERDAM",
                           "RIJKSMUSEUM AMSTERDAM", "HEMA AMSTERDAM"], "per_day": 3, "avg": D("38")},
            {"type": "duplicate_charge", "card": "K9003-ZD-01", "month": 8, "day": 14,
             "merchant": "IYZICO*KUZEY KIRTASIYE", "amount": D("1349.90")},
            {"type": "idle_boost", "account": "A9002-ZD-01", "months": [2, 3, 4], "extra_income": D("55000")},
        ],
    },
}
