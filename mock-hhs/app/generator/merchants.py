"""İşyeri açıklama metinleri (POS / sanal POS formatında, temsili).

Her kategori: (açıklama şablonları, ortalama işlem tutarı TL (2025 Ekim fiyatı), log-normal sigma, internet payı)
Şablondaki {n} rastgele şube/terminal numarası, {ilce} persona şehrine göre ilçe ile doldurulur.
"""

CATEGORIES = {
    "market": {
        "templates": ["MIGROS {n} {ilce} TR", "A101 YENI MAGAZACILIK {ilce}", "BIM BIRLESIK MAGAZALAR {ilce}",
                      "SOK MARKETLER {n} {ilce}", "CARREFOURSA {ilce} TR", "FILE MARKET {ilce}"],
        "online_templates": ["GETIR*BUYUK {n}", "MIGROS SANAL MARKET", "TRENDYOL GO*MARKET"],
        "avg": 520, "sigma": 0.7,
    },
    "yeme_icme": {
        "templates": ["STARBUCKS {ilce} {n}", "KAHVE DUNYASI {ilce}", "BURGER KING {n} {ilce}",
                      "SIMIT SARAYI {ilce}", "LOKANTA {ilce} {n}", "MADO {ilce}"],
        "online_templates": ["YEMEKSEPETI*SIPARIS", "GETIR YEMEK*{n}", "TRENDYOL YEMEK"],
        "avg": 380, "sigma": 0.6,
    },
    "akaryakit": {
        "templates": ["SHELL {ilce} {n}", "OPET {ilce} AKARYAKIT", "BP {ilce} {n}", "PETROL OFISI {ilce}"],
        "online_templates": [],
        "avg": 1900, "sigma": 0.35,
    },
    "ulasim": {
        "templates": ["ISTANBULKART DOLUM {n}", "BILETIX*ULASIM", "MARTI TEKNOLOJI", "BITAKSI*{n}"],
        "online_templates": ["PEGASUS HAVA YOLLARI", "THY ONLINE", "OBILET.COM"],
        "avg": 450, "sigma": 0.9,
    },
    "giyim": {
        "templates": ["LC WAIKIKI {ilce}", "DEFACTO {ilce} {n}", "KOTON {ilce}", "ZARA {ilce} AVM"],
        "online_templates": ["TRENDYOL.COM", "IYZICO*TRENDYOL", "HEPSIBURADA.COM"],
        "avg": 1400, "sigma": 0.6,
    },
    "elektronik": {
        "templates": ["MEDIAMARKT {ilce}", "TEKNOSA {ilce} {n}", "VATAN BILGISAYAR {ilce}"],
        "online_templates": ["HEPSIBURADA.COM", "AMAZON.COM.TR", "N11.COM"],
        "avg": 3500, "sigma": 0.8,
    },
    "saglik_kozmetik": {
        "templates": ["ECZANE {ilce} {n}", "GRATIS {ilce}", "WATSONS {ilce}", "ROSSMANN {ilce}"],
        "online_templates": ["TRENDYOL.COM", "FLO*ONLINE"],
        "avg": 450, "sigma": 0.6,
    },
    "eglence": {
        "templates": ["CINEMAXIMUM {ilce}", "PARAF*BOWLING {ilce}", "BILETIX*ETKINLIK"],
        "online_templates": ["STEAM PURCHASE", "PLAYSTATION NETWORK", "BILETIX.COM"],
        "avg": 600, "sigma": 0.6,
    },
    "ev_yasam": {
        "templates": ["KOCTAS {ilce}", "IKEA {ilce}", "ENGLISH HOME {ilce}"],
        "online_templates": ["HEPSIBURADA.COM", "TRENDYOL.COM"],
        "avg": 1200, "sigma": 0.7,
    },
    "seyahat": {
        "templates": ["OTEL {ilce} {n}", "AVIS RENT A CAR"],
        "online_templates": ["BOOKING.COM", "PEGASUS HAVA YOLLARI", "AIRBNB * HM{n}"],
        "avg": 4500, "sigma": 0.6,
    },
}

DISTRICTS = {
    "Manisa": ["YUNUSEMRE", "SEHZADELER", "MANISA"],
    "Istanbul": ["KADIKOY", "ATASEHIR", "BESIKTAS", "USKUDAR", "MALTEPE"],
    "Izmir": ["KONAK", "BORNOVA", "KARSIYAKA", "ALSANCAK"],
}

FOREIGN_TEMPLATES = ["AMAZON MKTPLACE PMTS", "UBER *TRIP", "APPLE.COM/BILL"]
