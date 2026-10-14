import re
import uuid
from datetime import datetime, timedelta, timezone
from decimal import Decimal as D

import pytest

from app.generator import params
from app.generator.personas import PERSONAS
from app.generator.store import dataset

from .test_consent_flow import client, consent_body, create, get_token, run_gkd  # noqa: F401

TR = timezone(timedelta(hours=3))
AMOUNT = re.compile(r"^-?\d{1,18}\.\d{1,5}$")


def hdr(token, psu="E", hhs="9001"):
    return {"X-Request-ID": str(uuid.uuid4()), "X-ASPSP-Code": hhs, "X-TPP-Code": "8001",
            "X-Access-Token": token, "PSU-Initiated": psu}


def connect(client, tckn="10000000214", hhs="9001", izin=None):
    body = consent_body(katilimciBlg={"hhsKod": hhs, "yosKod": "8001"},
                        kmlk={"kmlkTur": "K", "kmlkVrs": tckn, "ohkTur": "B"})
    if izin:
        body["hspBlg"]["iznBlg"]["iznTur"] = izin
    riza = create(client, body, hhs=hhs).json()["rzBlg"]["rizaNo"]
    q = run_gkd(client, riza, tckn=tckn)
    r = client.post("/erisim-belirteci", headers={"X-Request-ID": "x", "X-ASPSP-Code": hhs, "X-TPP-Code": "8001"},
                    json={"rizaNo": riza, "rizaTip": "H", "yetTip": "yet_kod", "yetKod": q["yetKod"][0]})
    return r.json()["erisimBelirteci"]


def win(days=30, end=None):
    end = end or datetime.now(TR)
    return {"hesapIslemBslTrh": (end - timedelta(days=days)).isoformat(), "hesapIslemBtsTrh": end.isoformat()}


# ------------------------------------------------------------------ hesap / bakiye
def test_accounts_and_balance(client):
    tok = connect(client)
    r = client.get("/hesaplar", headers=hdr(tok))
    assert r.status_code == 200, r.text
    refs = {a["hspTml"]["hspRef"] for a in r.json()}
    assert refs == {"A9001-MK-01", "A9001-MK-02"}
    acc = r.json()[0]
    assert acc["hspTml"]["hspNo"].startswith("TR") and len(acc["hspTml"]["hspNo"]) == 26
    assert "hspDty" in acc  # 02 izni var

    r = client.get("/bakiye", headers=hdr(tok))
    assert r.status_code == 200
    for b in r.json():
        assert AMOUNT.match(b["bky"]["bkyTtr"])


def test_kmh_account_reports_used_credit(client):
    tok = connect(client, hhs="9003")
    b = client.get("/hesaplar/A9003-MK-01/bakiye", headers=hdr(tok, hhs="9003")).json()
    assert "krdHsp" in b and AMOUNT.match(b["krdHsp"]["kulKrdTtr"])


# ------------------------------------------------------------------ işlemler
def test_transactions_window_and_detail(client):
    tok = connect(client)
    r = client.get("/hesaplar/A9001-MK-01/islemler", headers=hdr(tok), params={**win(30), "syfKytSayi": 100})
    assert r.status_code == 200, r.text
    items = r.json()["isller"]
    assert items and int(r.headers["X-Total-Count"]) == len(items)
    t = items[0]["islTml"]
    assert AMOUNT.match(t["islTtr"]) and t["brcAlc"] in ("A", "B") and "islAcklm" in t  # 05 izni var
    # maaş islAmc=05 ile gelir
    r = client.get("/hesaplar/A9001-MK-01/islemler", headers=hdr(tok), params={**win(31), "brcAlc": "A", "syfKytSayi": 100})
    assert any(i["islTml"].get("islAmc") == "05" for i in r.json()["isller"])


def test_psu_window_limits(client):
    tok = connect(client)
    r = client.get("/hesaplar/A9001-MK-01/islemler", headers=hdr(tok), params=win(40))
    assert r.status_code == 400 and r.json()["errorCode"].endswith("InvalidStartEndTime")
    r = client.get("/hesaplar/A9001-MK-01/islemler", headers=hdr(tok, psu="H"), params=win(2))
    assert r.status_code == 400


def test_system_query_quota_4_per_day(client):
    tok = connect(client)
    for _ in range(4):
        assert client.get("/hesaplar/A9001-MK-01/islemler", headers=hdr(tok, psu="H"), params=win(1)).status_code == 200
    r = client.get("/hesaplar/A9001-MK-01/islemler", headers=hdr(tok, psu="H"), params=win(1))
    assert r.status_code == 429 and r.json()["errorCode"].endswith("ExceededRate")
    # ÖHK tetikli sorgular kotaya takılmaz
    assert client.get("/hesaplar/A9001-MK-01/islemler", headers=hdr(tok), params=win(5)).status_code == 200


def test_basic_transaction_permission_hides_detail(client):
    tok = connect(client, izin=["01", "04"])
    r = client.get("/hesaplar/A9001-MK-01/islemler", headers=hdr(tok), params=win(20))
    t = r.json()["isller"][0]
    assert "islAcklm" not in t["islTml"] and "islDty" not in t
    assert client.get("/bakiye", headers=hdr(tok)).status_code == 403  # 03 yok


# ------------------------------------------------------------------ kartlar
def test_cards_detail_and_movements(client):
    tok = connect(client)
    cards = client.get("/kartlar", headers=hdr(tok)).json()
    assert [c["kartRef"] for c in cards] == ["K9001-MK-01"]
    det = client.get("/kartlar/K9001-MK-01/kart-detay", headers=hdr(tok)).json()
    assert det["limitTutari"] == "120000.00"
    borc, asg = D(det["donemBorcu"]), D(det["asgariOdeme"])
    assert asg == (borc * params.MIN_PAYMENT_RATE_HIGH).quantize(D("0.01"))  # limit > 100.000 → %40
    mv = client.get("/kartlar/K9001-MK-01/kart-hareketleri", headers=hdr(tok), params=win(30)).json()
    assert mv["kartHareketleri"]


def test_unselected_or_foreign_resources_hidden(client):
    tok = connect(client)
    assert client.get("/hesaplar/A9003-MK-01", headers=hdr(tok)).status_code == 404  # başka banka
    assert client.get("/kartlar/K9003-MK-01/kart-detay", headers=hdr(tok)).status_code == 404


def test_requires_token(client):
    r = client.get("/hesaplar", headers={"X-Request-ID": "1", "X-ASPSP-Code": "9001", "X-TPP-Code": "8001",
                                         "PSU-Initiated": "E"})
    assert r.status_code == 401


# ------------------------------------------------------------------ sentetik verinin gerçekçiliği
@pytest.mark.parametrize("tckn", list(PERSONAS))
def test_bddk_limit_cap_and_min_payment(tckn):
    sim = dataset(tckn)
    p = PERSONAS[tckn]
    cap = p["declared_monthly_income"] * params.LIMIT_MULTIPLIER_LATER
    assert sum(c.limit for c in sim.cards.values()) <= cap
    for card in sim.cards.values():
        rate = params.min_payment_rate(card.limit)
        for st in card.statements:
            assert st["asgariOdeme"] == (st["donemBorcu"] * rate).quantize(D("0.01"))
        # faiz ve vergiler limiti en fazla küçük bir oranda aşabilir (limit aşımı)
        assert card.used <= card.limit * D("1.05")


def test_scenarios_present():
    ayse, mehmet, zeynep = (dataset(t) for t in PERSONAS)
    # Ayşe: gecikmeli ödeme ve sadece asgari ödeme → faiz satırı
    assert any(m["islemTuru"] == "FAIZ" for m in ayse.cards["K9001-AY-01"].movements)
    assert any(m["taksitSayisi"] == 6 for m in ayse.cards["K9001-AY-01"].movements)
    # Mehmet: KMH faizi ve banka masrafı
    marmara = mehmet.accounts["A9003-MK-01"].txs
    assert any(t["islAcklm"] == "KMH FAIZ TAHAKKUKU" for t in marmara)
    assert any(t["islAcklm"] == "HESAP ISLETIM UCRETI" for t in marmara)
    # Zeynep: döviz harcaması ve mükerrer çekim
    gold = zeynep.cards["K9002-ZD-01"].movements
    assert any(m["dovizBilgisi"] and m["dovizBilgisi"]["orijinalParaBirimi"] == "USD" for m in gold)
    bonus = [m for m in zeynep.cards["K9003-ZD-01"].movements if m["islemAciklama"] == "IYZICO*KUZEY KIRTASIYE"]
    assert len(bonus) == 2


def test_deterministic():
    from datetime import date
    from app.generator.engine import Simulator
    t = "10000000382"
    a = Simulator(t, PERSONAS[t], date(2026, 10, 1)).run()
    b = Simulator(t, PERSONAS[t], date(2026, 10, 1)).run()
    assert [x["islTtr"] for x in a.accounts["A9002-ZD-01"].txs] == [x["islTtr"] for x in b.accounts["A9002-ZD-01"].txs]
