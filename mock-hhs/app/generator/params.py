"""Düzenleyici ve kalibrasyon parametreleri.

Kaynaklar ve açıklamalar: docs/mock-veri-parametreleri.md
Mevzuat değişirse yalnızca bu dosya güncellenir.
"""
from decimal import Decimal as D

# --- TCMB azami faiz oranları (aylık, %), 01.10.2026'dan itibaren ---
CARD_RATE_TIERS = [
    # (dönem borcu üst sınırı, akdi, gecikme)
    (D("30000"), D("3.25"), D("3.55")),
    (D("180000"), D("3.75"), D("4.05")),
    (None, D("4.25"), D("4.55")),
]
CASH_ADVANCE_RATE = D("4.25")     # nakit avans / nakit kullanım, akdi
KMH_RATE = D("4.25")              # KMH azami akdi (TCMB: nakit kullanım ile aynı)
KMH_LATE_RATE = D("4.55")

# --- Vergiler (faiz üzerinden) ---
BSMV = D("0.15")
KKDF = D("0.15")
APPLY_TAX_ON_KMH = True  # varsayım, doğrulanacak

# --- BDDK asgari ödeme (Ekim 2026) ---
MIN_PAYMENT_LIMIT_THRESHOLD = D("100000")
MIN_PAYMENT_RATE_LOW = D("0.20")   # limit <= eşik
MIN_PAYMENT_RATE_HIGH = D("0.40")  # limit > eşik

# --- BDDK toplam kart limiti tavanı (aylık net gelirin katı) ---
LIMIT_MULTIPLIER_FIRST_YEAR = 2
LIMIT_MULTIPLIER_LATER = 4

# --- Diğer ---
FAST_LIMIT = D("100000")
STATEMENT_TO_DUE_DAYS = 10  # hesap kesim → son ödeme (piyasa uygulaması)


def card_rates(period_debt: D) -> tuple[D, D]:
    """Dönem borcuna göre (akdi, gecikme) aylık faiz oranı, yüzde olarak."""
    for upper, akdi, gecikme in CARD_RATE_TIERS:
        if upper is None or period_debt < upper:
            return akdi, gecikme
    raise AssertionError


def min_payment_rate(card_limit: D) -> D:
    return MIN_PAYMENT_RATE_LOW if card_limit <= MIN_PAYMENT_LIMIT_THRESHOLD else MIN_PAYMENT_RATE_HIGH


def with_taxes(interest: D) -> D:
    return interest * (1 + BSMV + KKDF)
