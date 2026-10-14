"""12 aylık sentetik hesap ve kart verisi üreten günlük simülasyon motoru.

Deterministiktir: aynı persona + aynı bitiş günü → aynı veri.
Kurallar params.py'den (TCMB/BDDK), tutarlar personas.py'den gelir.
"""
from __future__ import annotations

import calendar
import hashlib
import random
from dataclasses import dataclass, field
from datetime import date, datetime, time, timedelta
from decimal import ROUND_HALF_UP, Decimal as D

from ..config import TR_TZ
from . import params
from .merchants import CATEGORIES, DISTRICTS, FOREIGN_TEMPLATES
from .personas import FX_PATH, MONTHLY_PRICE_DRIFT

CENT = D("0.01")
SEASON = {
    "electric": {1: 1.15, 2: 1.05, 3: 0.95, 4: 0.85, 5: 0.9, 6: 1.25, 7: 1.55, 8: 1.6, 9: 1.2, 10: 0.9, 11: 0.95, 12: 1.1},
    "gas": {1: 2.5, 2: 2.2, 3: 1.6, 4: 1.0, 5: 0.5, 6: 0.3, 7: 0.25, 8: 0.25, 9: 0.35, 10: 0.7, 11: 1.4, 12: 2.2},
}


DISCRETIONARY = {"yeme_icme", "eglence", "giyim", "ev_yasam", "seyahat", "elektronik"}


def q(x) -> D:
    return D(str(x)).quantize(CENT, rounding=ROUND_HALF_UP)


# ------------------------------------------------------------------ durum nesneleri
@dataclass
class AccountState:
    meta: dict
    balance: D
    kmh_limit: D
    txs: list = field(default_factory=list)
    neg_accum: D = D("0")          # ay içi günlük negatif bakiye toplamı (KMH faizi için)
    daily_balance: dict = field(default_factory=dict)

    @property
    def available(self) -> D:
        return self.balance + self.kmh_limit

    def balance_at(self, when: datetime) -> D:
        last = self.meta["initial"]
        for tx in self.txs:
            if tx["islGrckZaman"] > when:
                break
            last = tx["gnclBky"]
        return last


@dataclass
class InstallmentPlan:
    merchant: str
    total: D
    n: int
    per: D
    billed: int = 0
    purchase_date: date | None = None


@dataclass
class CardState:
    meta: dict
    limit: D
    movements: list = field(default_factory=list)
    statements: list = field(default_factory=list)
    unbilled: D = D("0")
    carried: D = D("0")            # kesilmiş ama ödenmemiş borç
    plans: list = field(default_factory=list)

    @property
    def used(self) -> D:
        remaining_inst = sum((p.per * (p.n - p.billed) for p in self.plans), D("0"))
        return self.carried + self.unbilled + remaining_inst

    @property
    def available(self) -> D:
        return self.limit - self.used


# ------------------------------------------------------------------ motor
class Simulator:
    def __init__(self, tckn: str, persona: dict, end: date, days: int = 365):
        self.tckn = tckn
        self.p = persona
        self.end = end
        self.start = end - timedelta(days=days)
        seed = int(hashlib.sha256(f"{tckn}:{end.isoformat()}".encode()).hexdigest()[:12], 16)
        self.rng = random.Random(seed)
        self.seq = 0
        self.accounts = {a["ref"]: AccountState(a, a["initial"], a["kmh_limit"]) for a in persona["accounts"]}
        self.cards = {c["ref"]: CardState(c, c.get("limit", D("0"))) for c in persona["cards"] if c["kartTipi"] != "B"}
        self.debit_cards = {c["ref"]: c for c in persona["cards"] if c["kartTipi"] == "B"}
        self.districts = DISTRICTS[persona["city"]]
        self.irregular_plan: dict = {}
        self._check_limits()

    # ---------- yardımcılar
    def _check_limits(self):
        mult = params.LIMIT_MULTIPLIER_FIRST_YEAR if self.p["card_years"] < 1 else params.LIMIT_MULTIPLIER_LATER
        total = sum((c.limit for c in self.cards.values()), D("0"))
        cap = self.p["declared_monthly_income"] * mult
        assert total <= cap, f"{self.p['name']}: toplam kart limiti {total} > BDDK tavanı {cap}"

    def month_index(self, d: date) -> int:
        return (d.year - self.start.year) * 12 + d.month - self.start.month

    def drift(self, d: date) -> D:
        return (1 + MONTHLY_PRICE_DRIFT) ** self.month_index(d)

    def fx(self, ccy: str, d: date) -> D:
        a, b = FX_PATH[ccy]
        frac = D((d - self.start).days) / D(max(1, (self.end - self.start).days))
        return (a + (b - a) * frac).quantize(D("0.0001"))

    def ts(self, d: date, lo=8, hi=23) -> datetime:
        h = self.rng.randint(lo, hi - 1)
        return datetime.combine(d, time(h, self.rng.randint(0, 59), self.rng.randint(0, 59)), TR_TZ)

    def next_no(self, d: date) -> str:
        self.seq += 1
        return f"{d:%Y%m%d}{self.seq:07d}"

    def ref_no(self) -> str:
        return f"R{self.rng.randint(10**9, 10**10 - 1)}"

    @staticmethod
    def clamp_day(y: int, m: int, day: int) -> date:
        return date(y, m, min(day, calendar.monthrange(y, m)[1]))

    def fill(self, tmpl: str) -> str:
        return tmpl.format(n=self.rng.randint(1000, 9999), ilce=self.rng.choice(self.districts))

    # ---------- hesap hareketi
    def acc_tx(self, ref: str, when: datetime, amount: D, brc: str, islTur: str, desc: str, *,
               islAmc=None, kanal="K", karsi=None, karsi_iban=None, ohk_msj=None, force=False) -> bool:
        acc = self.accounts[ref]
        amount = q(amount)
        if amount <= 0:
            return False
        if brc == "B":
            if amount > acc.available and not force:
                return False
            acc.balance -= amount
        else:
            acc.balance += amount
        acc.txs.append({
            "islNo": self.next_no(when.date()), "refNo": self.ref_no(), "islTtr": amount,
            "prBrm": acc.meta["currency"], "islGrckZaman": when, "kanal": kanal, "brcAlc": brc,
            "islTur": islTur, "islAmc": islAmc, "islAcklm": desc, "ohkMsj": ohk_msj,
            "karsiUnvan": karsi, "karsiIban": karsi_iban, "gnclBky": q(acc.balance),
        })
        return True

    # ---------- kart hareketi
    def card_purchase(self, ref: str, when: datetime, amount_try: D, merchant: str, *, online=False,
                      islemTuru="ALISVERIS", fx=None, taksit=None) -> bool:
        if ref in self.debit_cards:
            acc_ref = self.debit_cards[ref]["linked_account"]
            return self.acc_tx(acc_ref, when, amount_try, "B", "UYE_ISYERI_ISLEMLERI", merchant, kanal="D")
        card = self.cards[ref]
        amount_try = q(amount_try)
        if amount_try > card.available:
            return False  # limit yetersiz → işlem reddedildi
        if taksit:
            n = taksit
            per = q(amount_try / n)
            card.plans.append(InstallmentPlan(merchant, amount_try, n, per, purchase_date=when.date()))
        else:
            card.unbilled += amount_try
        card.movements.append({
            "islemNo": self.next_no(when.date()), "islemTarihi": when, "islemTutari": amount_try,
            "islemParaBirimi": "TRY", "taksitSayisi": taksit, "islemTuru": "TAKSITLI_ALISVERIS" if taksit else islemTuru,
            "islemAciklama": merchant, "uyeIsyeri": merchant, "internet": online, "dovizBilgisi": fx,
            "referansNo": self.ref_no(), "borcAlacak": "B",
        })
        return True

    def card_line(self, ref: str, d: date, amount: D, kind: str, desc: str, brc="B"):
        card = self.cards[ref]
        card.movements.append({
            "islemNo": self.next_no(d), "islemTarihi": datetime.combine(d, time(23, 59), TR_TZ),
            "islemTutari": q(amount), "islemParaBirimi": "TRY", "taksitSayisi": None, "islemTuru": kind,
            "islemAciklama": desc, "uyeIsyeri": None, "internet": False, "dovizBilgisi": None,
            "referansNo": self.ref_no(), "borcAlacak": brc,
        })

    # ---------- ana döngü
    def run(self) -> "Simulator":
        d = self.start
        while d <= self.end:
            self.day(d)
            for acc in self.accounts.values():
                if acc.balance < 0:
                    acc.neg_accum += -acc.balance
                acc.daily_balance[d] = acc.balance
            if d.day == calendar.monthrange(d.year, d.month)[1]:
                self.month_end(d)
            d += timedelta(days=1)
        self.finalize()
        return self

    def finalize(self):
        """Hareketleri zamana göre sırala, işlem numaralarını ve güncel bakiyeleri yeniden üret."""
        for acc in self.accounts.values():
            acc.txs.sort(key=lambda t: t["islGrckZaman"])
            bal = acc.meta["initial"]
            for i, tx in enumerate(acc.txs, 1):
                bal += tx["islTtr"] if tx["brcAlc"] == "A" else -tx["islTtr"]
                tx["gnclBky"] = q(bal)
                tx["islNo"] = f"{tx['islGrckZaman']:%Y%m%d}{acc.meta['ref'][-5:].replace('-', '')}{i:05d}"
        for card in self.cards.values():
            card.movements.sort(key=lambda m: m["islemTarihi"])
            for i, mv in enumerate(card.movements, 1):
                mv["islemNo"] = f"{mv['islemTarihi']:%Y%m%d}{card.meta['last4']}{i:05d}"

    def day(self, d: date):
        m = self.month_index(d)
        self.incomes(d, m)
        self.fixed(d, m)
        self.subscriptions(d, m)
        self.events(d, m)
        self.spending(d)
        for ref, card in self.cards.items():
            if d.day == min(card.meta["statement_day"], calendar.monthrange(d.year, d.month)[1]):
                self.statement(ref, d)
        self.card_payments(d, m)

    # ---------- gelirler
    def incomes(self, d: date, m: int):
        for inc in self.p["incomes"]:
            if inc.get("irregular"):
                key = (d.year, d.month)
                if key not in self.irregular_plan:
                    lo, hi = inc["per_month"]
                    k = self.rng.randint(lo, hi)
                    days = sorted(self.rng.sample(range(1, calendar.monthrange(d.year, d.month)[1] + 1), k))
                    self.irregular_plan[key] = days
                for _ in [x for x in self.irregular_plan[key] if x == d.day]:
                    lo, hi = inc["range"]
                    amt = D(self.rng.randint(int(lo / 50), int(hi / 50)) * 50)
                    no = f"{d.year}/{self.rng.randint(1, 250):03d}"
                    self.acc_tx(inc["account"], self.ts(d, 9, 18), amt, "A", inc["islTur"],
                                f"{inc['islTur']} - {self.rng.choice(inc['from_pool'])} - {inc['desc'].format(no=no)}",
                                islAmc=inc["islAmc"], kanal="D", karsi=None)
                continue
            if "months_active" in inc and m not in inc["months_active"]:
                continue
            target = self.clamp_day(d.year, d.month, inc["day"])
            if inc.get("jitter"):
                rnd = random.Random(f"{self.tckn}{inc['desc']}{d.year}{d.month}")
                target = self.clamp_day(d.year, d.month, inc["day"] + rnd.randint(0, inc["jitter"]))
            if d != target:
                continue
            amt = inc["amount"]
            if inc.get("raise") and m >= inc["raise"]["month"]:
                amt = inc["raise"]["amount"]
            if inc.get("vary"):
                amt = amt * (1 + D(str(self.rng.uniform(-float(inc["vary"]), float(inc["vary"])))))
                amt = D(int(amt / 50) * 50)
            self.acc_tx(inc["account"], self.ts(d, 9, 12), amt, "A", inc["islTur"],
                        f"{inc['islTur']} - {inc['from']} - {inc['desc']}", islAmc=inc["islAmc"], kanal="D",
                        karsi=inc["from"])

    # ---------- düzenli giderler
    def fixed(self, d: date, m: int):
        for f in self.p["fixed"]:
            if "months_active" in f and m not in f["months_active"]:
                continue
            if d != self.clamp_day(d.year, d.month, f["day"]):
                continue
            amt = f["amount"]
            if f.get("raise") and m >= f["raise"]["month"]:
                amt = f["raise"]["amount"]
            if f.get("drift"):
                amt = amt * self.drift(d)
            if f.get("seasonal"):
                amt = amt * D(str(SEASON[f["seasonal"]][d.month]))
            if f.get("vary"):
                amt = amt * (1 + D(str(self.rng.uniform(-float(f["vary"]), float(f["vary"])))))
            if "sweep_keep" in f:
                amt = max(D("0"), self.accounts[f["account"]].balance - f["sweep_keep"])
            if f.get("cover_negative"):
                dest = self.accounts[f["own_transfer_to"]]
                amt = amt + max(D("0"), -dest.balance)
            amt = q(amt)
            if f["islTur"] in ("FAST", "EFT", "HAVALE") and not f.get("own_transfer_to"):
                desc = f"{f['islTur']} GIDEN - {f['to']} - {f['desc']}"
            elif f.get("own_transfer_to"):
                desc = f"{f['islTur']} - {f['desc']}"
            else:
                desc = f"{f['to']} {f['desc']}"
            when = self.ts(d, 9, 20)
            kanal = "I" if f["islTur"] == "KURUM_FATURA_ODEMESI" else "K"
            iban = f.get("iban")
            masked = f"{iban[:4]}***************{iban[-4:]}" if iban else None
            ok = self.acc_tx(f["account"], when, amt, "B", f["islTur"], desc, islAmc=f["islAmc"], kanal=kanal,
                             karsi=f["to"], karsi_iban=masked)
            if ok and f.get("own_transfer_to"):
                self.acc_tx(f["own_transfer_to"], when + timedelta(seconds=5), amt, "A", f["islTur"],
                            f"{f['islTur']} - {f['to']} - {f['desc']}", kanal="K", karsi=f["to"])
            if ok and f.get("fee"):
                self.acc_tx(f["account"], when + timedelta(seconds=1), f["fee"], "B", "UCRET_KOMISYON_FAIZ",
                            "EFT UCRETI", kanal="D", force=True)

    # ---------- abonelikler
    def subscriptions(self, d: date, m: int):
        for s in self.p["subscriptions"]:
            if d != self.clamp_day(d.year, d.month, s["day"]):
                continue
            amt = s["amount"]
            if s.get("price_change") and m >= s["price_change"]["month"]:
                amt = s["price_change"]["amount"]
            if s.get("drift"):
                amt = amt * self.drift(d)
            ccy = s.get("currency", "TRY")
            fx = None
            if ccy != "TRY":
                rate = self.fx(ccy, d)
                fx = {"orijinalTutar": q(amt), "orijinalParaBirimi": ccy, "kur": rate}
                amt = amt * rate
            self.card_purchase(s["card"], self.ts(d, 0, 6), amt, self.fill(s["merchant"]), online=True, fx=fx)

    # ---------- özel olaylar
    def events(self, d: date, m: int):
        for e in self.p["events"]:
            t = e["type"]
            if t == "installment" and m == e["month"] and d.day == e["day"]:
                self.card_purchase(e["card"], self.ts(d, 11, 20), e["total"], e["merchant"], taksit=e["n"])
            elif t == "foreign_trip" and m == e["month"] and e["day"] <= d.day < e["day"] + e["days"]:
                for _ in range(e["per_day"]):
                    orig = q(e["avg"] * D(str(self.rng.lognormvariate(0, 0.5))))
                    rate = self.fx(e["currency"], d)
                    self.card_purchase(e["card"], self.ts(d, 9, 23), orig * rate, self.rng.choice(e["merchants"]),
                                       islemTuru="YURTDISI_ALISVERIS",
                                       fx={"orijinalTutar": orig, "orijinalParaBirimi": e["currency"], "kur": rate})
            elif t == "duplicate_charge" and m == e["month"] and d.day == e["day"]:
                when = self.ts(d, 12, 18)
                self.card_purchase(e["card"], when, e["amount"], e["merchant"], online=True)
                self.card_purchase(e["card"], when + timedelta(minutes=2), e["amount"], e["merchant"], online=True)
            elif t == "fee" and d.day == e["day"] and m % e["every_months"] == e["every_months"] - 1:
                self.acc_tx(e["account"], self.ts(d, 1, 4), e["amount"], "B", "UCRET_KOMISYON_FAIZ", e["desc"], kanal="D",
                            force=True)
            elif t == "idle_boost" and m in e["months"] and d.day == 20:
                self.acc_tx(e["account"], self.ts(d, 10, 16), e["extra_income"], "A", "EFT",
                            "EFT - MAVI DUZ TASARIM AS - kurumsal kimlik projesi", islAmc="06", kanal="D")

    # ---------- rastgele harcamalar
    def spending(self, d: date):
        weekend = d.weekday() >= 5
        for cat, (per_month, mult, online_share, targets) in self.p["spending"].items():
            info = CATEGORIES[cat]
            lam = float(per_month) / 30.0
            if weekend and cat in ("yeme_icme", "eglence", "giyim", "ev_yasam"):
                lam *= 1.5
            # Bütçe farkındalığı: kart limiti %60'tan fazla doluysa isteğe bağlı harcama yarıya iner
            if cat in DISCRETIONARY:
                credit = [self.cards[r] for r in targets if r in self.cards]
                if credit and all(c.used > c.limit * D("0.6") for c in credit):
                    lam *= 0.5
            n = sum(1 for _ in range(4) if self.rng.random() < lam / 4)
            for _ in range(n):
                online = self.rng.random() < online_share and info["online_templates"]
                tmpl = self.rng.choice(info["online_templates"] if online else info["templates"])
                amount = D(info["avg"]) * mult * self.drift(d) * D(str(self.rng.lognormvariate(0, info["sigma"])))
                amount = max(D("15"), q(amount))
                ref = self.rng.choices(list(targets), weights=list(targets.values()))[0]
                self.card_purchase(ref, self.ts(d, 7, 24 if not online else 24), amount, self.fill(tmpl), online=bool(online))

    # ---------- hesap kesimi
    def statement(self, ref: str, d: date):
        card = self.cards[ref]
        prev = card.statements[-1] if card.statements else None
        interest = D("0")
        if prev:
            unpaid = prev["donemBorcu"] - prev["odenen"]
            akdi, gecikme = params.card_rates(prev["donemBorcu"])
            if unpaid > 0:
                below_min = max(D("0"), prev["asgariOdeme"] - prev["odenen"])
                interest += (unpaid - below_min) * akdi / 100 + below_min * gecikme / 100
            elif prev["odemeTarihi"] and prev["odemeTarihi"] > prev["sonOdemeTarihi"]:
                late = (prev["odemeTarihi"] - prev["sonOdemeTarihi"]).days
                interest += prev["donemBorcu"] * gecikme / 100 * D(late) / 30
        if interest > 0:
            interest = q(interest)
            self.card_line(ref, d, interest, "FAIZ", "AKDI/GECIKME FAIZI")
            self.card_line(ref, d, q(interest * params.BSMV), "VERGI", "BSMV")
            self.card_line(ref, d, q(interest * params.KKDF), "VERGI", "KKDF")
            interest = interest + q(interest * params.BSMV) + q(interest * params.KKDF)
        fee = D("0")
        if card.meta.get("annual_fee") and self.month_index(d) == card.meta.get("annual_fee_month"):
            fee = card.meta["annual_fee"]
            self.card_line(ref, d, fee, "UCRET", "KART YILLIK UYELIK UCRETI")
            self.card_line(ref, d, q(fee * params.BSMV), "VERGI", "BSMV")
            fee = fee + q(fee * params.BSMV)
        inst = D("0")
        for p in card.plans:
            if p.billed < p.n and p.purchase_date < d:
                p.billed += 1
                inst += p.per
        card.plans = [p for p in card.plans if p.billed < p.n]
        donem = q(card.unbilled + inst + card.carried + interest + fee)
        rate = params.min_payment_rate(card.limit)
        st = {
            "hesapKesimTarihi": d, "sonOdemeTarihi": d + timedelta(days=params.STATEMENT_TO_DUE_DAYS),
            "donemBorcu": donem, "asgariOdeme": q(donem * rate), "asgariOran": rate,
            "odenen": D("0"), "odemeTarihi": None,
        }
        card.statements.append(st)
        card.carried = donem
        card.unbilled = D("0")

    # ---------- kart ödemeleri
    def card_payments(self, d: date, m: int):
        for ref, card in self.cards.items():
            if not card.statements:
                continue
            st = card.statements[-1]
            if st["odenen"] > 0 or st["donemBorcu"] <= 0:
                continue
            late = next((e for e in self.p["events"] if e["type"] == "late_payment" and e["card"] == ref
                         and self.month_index(st["hesapKesimTarihi"]) == e["month"]), None)
            min_only = next((e for e in self.p["events"] if e["type"] == "min_payment_only" and e["card"] == ref
                             and self.month_index(st["hesapKesimTarihi"]) == e["month"]), None)
            pay_day = st["sonOdemeTarihi"] + timedelta(days=late["days_late"]) if late else st["sonOdemeTarihi"] - timedelta(days=1)
            if d != pay_day:
                continue
            acc = self.accounts[card.meta["pay_from"]]
            target = st["asgariOdeme"] if min_only else st["donemBorcu"]
            amount = min(target, max(D("0"), acc.available))
            if amount <= 0:
                continue
            when = self.ts(d, 9, 21)
            if self.acc_tx(card.meta["pay_from"], when, amount, "B", "KK_ODEMESI",
                           f"KREDI KARTI ODEMESI {card.meta['product'].upper()} ****{card.meta['last4']}", kanal="K"):
                st["odenen"] = amount
                st["odemeTarihi"] = d
                card.carried -= amount
                self.card_line(ref, d, amount, "ODEME", "ODEMENIZ ICIN TESEKKUR EDERIZ", brc="A")

    # ---------- ay sonu: KMH faizi
    def month_end(self, d: date):
        for ref, acc in self.accounts.items():
            if acc.neg_accum > 0 and acc.kmh_limit > 0:
                interest = q(acc.neg_accum / 30 * params.KMH_RATE / 100)
                if interest > 0:
                    when = datetime.combine(d, time(23, 30), TR_TZ)
                    self.acc_tx(ref, when, interest, "B", "UCRET_KOMISYON_FAIZ", "KMH FAIZ TAHAKKUKU", kanal="D", force=True)
                    if params.APPLY_TAX_ON_KMH:
                        self.acc_tx(ref, when + timedelta(seconds=1), q(interest * params.BSMV), "B",
                                    "UCRET_KOMISYON_FAIZ", "KMH FAIZ BSMV", kanal="D", force=True)
                        self.acc_tx(ref, when + timedelta(seconds=2), q(interest * params.KKDF), "B",
                                    "UCRET_KOMISYON_FAIZ", "KMH FAIZ KKDF", kanal="D", force=True)
            acc.neg_accum = D("0")
