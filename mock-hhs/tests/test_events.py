import asyncio
import re
import uuid
from datetime import timedelta

import pytest

from app import config, events

from .test_consent_flow import TCKN, client, consent_body, create, get_token, run_gkd  # noqa: F401

HHS = "9002"  # Ege Bank — bu testler kendi bankasında abonelik açar
ZEYNEP = "10000000382"


def h(**extra):
    return {"X-Request-ID": str(uuid.uuid4()), "X-ASPSP-Code": HHS, "X-TPP-Code": "8001", **extra}


ALL_TYPES = [
    {"olayTipi": "AYRIK_GKD_BASARILI", "kaynakTipi": "HESAP_BILGISI_RIZASI"},
    {"olayTipi": "AYRIK_GKD_BASARISIZ", "kaynakTipi": "HESAP_BILGISI_RIZASI"},
    {"olayTipi": "KAYNAK_GUNCELLENDI", "kaynakTipi": "HESAP_BILGISI_RIZASI"},
    {"olayTipi": "KAYNAK_GUNCELLENDI", "kaynakTipi": "BAKIYE"},
]


@pytest.fixture(scope="module")
def sink():
    """Olayları HTTP yerine bir listeye toplar."""
    got = []
    original = events.sender

    async def fake(url, body):
        got.append(body)
        return 202

    events.sender = fake
    yield got
    events.sender = original


def tick():
    asyncio.run(events.tick())


def ayrik_body():
    b = consent_body(katilimciBlg={"hhsKod": HHS, "yosKod": "8001"},
                     kmlk={"kmlkTur": "K", "kmlkVrs": ZEYNEP, "ohkTur": "B"})
    b["gkd"] = {"yetYntm": "A", "yonAdr": "fincopilot://gkd", "ayrikGkd": {"ohkTanimTip": "TCKN", "ohkTanimDeger": ZEYNEP}}
    b["hspBlg"]["iznBlg"]["iznTur"] = ["01", "02", "03", "04", "05", "06", "07", "08", "09"]
    return b


def approve_in_bank_app(client, riza_no, action="onay"):
    r = client.post("/banka-uygulamasi/giris", data={"hhsKod": HHS, "tckn": ZEYNEP, "sifre": "Test1234"})
    assert r.status_code == 303
    page = client.get(r.headers["location"])
    sid = re.search(r'name="sid" value="([^"]+)"', page.text).group(1)
    assert riza_no in page.text
    refs = re.findall(r'name="(hspRef|kartRef)" value="([^"]+)"', page.text)
    r = client.post("/banka-uygulamasi/onay", data={
        "sid": sid, "rizaNo": riza_no, "islem": action,
        "hspRef": [v for k, v in refs if k == "hspRef"], "kartRef": [v for k, v in refs if k == "kartRef"]})
    assert r.status_code == 303


def test_subscription_crud(client):
    body = {"katilimciBlg": {"hhsKod": "9003", "yosKod": "8001"}, "abonelikTipleri": ALL_TYPES[:1]}
    r = client.post("/olay-abonelik", json=body, headers={**h(), "X-ASPSP-Code": "9003"})
    assert r.status_code == 201
    no = r.json()["olayAbonelikNo"]
    assert client.post("/olay-abonelik", json=body, headers={**h(), "X-ASPSP-Code": "9003"}).status_code == 400
    body["abonelikTipleri"] = ALL_TYPES
    r = client.put(f"/olay-abonelik/{no}", json=body, headers={**h(), "X-ASPSP-Code": "9003"})
    assert len(r.json()["abonelikTipleri"]) == 4
    assert client.delete(f"/olay-abonelik/{no}", headers={**h(), "X-ASPSP-Code": "9003"}).status_code == 204


def test_ayrik_requires_subscription(client):
    r = client.post("/hesap-bilgisi-rizasi", json=ayrik_body(), headers=h())
    assert r.status_code == 400 and "abonelik" in r.json()["moreInformationTr"]


def test_ayrik_flow_and_events(client, sink):
    r = client.post("/olay-abonelik", json={"katilimciBlg": {"hhsKod": HHS, "yosKod": "8001"},
                                            "abonelikTipleri": ALL_TYPES}, headers=h())
    assert r.status_code == 201
    sub_no = r.json()["olayAbonelikNo"]

    # 1) ayrık rıza: banka sayfa adresi yok
    r = client.post("/hesap-bilgisi-rizasi", json=ayrik_body(), headers=h())
    assert r.status_code == 201, r.text
    riza = r.json()["rzBlg"]["rizaNo"]
    assert "hhsYonAdr" not in r.json()["gkd"]

    # 2) kullanıcı banka uygulamasında onaylar → AYRIK_GKD_BASARILI
    approve_in_bank_app(client, riza)
    tick()
    assert any(e["olayTipi"] == "AYRIK_GKD_BASARILI" and e["kaynakNo"] == riza for e in sink)

    # 3) YÖS rızayı sorgular, yetKod'u alır, token'a çevirir
    rz = client.get(f"/hesap-bilgisi-rizasi/{riza}", headers=h()).json()
    assert rz["rzBlg"]["rizaDrm"] == "Y" and rz["gkd"]["yetKod"]
    tok = client.post("/erisim-belirteci", headers=h(), json={"rizaNo": riza, "rizaTip": "H", "yetTip": "yet_kod",
                                                               "yetKod": rz["gkd"]["yetKod"]}).json()["erisimBelirteci"]

    # 4) ilk tarama başlangıç noktasını kaydeder; yeni hareket → BAKIYE olayı
    tick()
    before = client.get("/hesaplar/A9002-ZD-01/bakiye", headers=h(**{"X-Access-Token": tok, "PSU-Initiated": "E"})).json()
    r = client.post("/_mock/simulasyon/islem", json={"tckn": ZEYNEP, "hspRef": "A9002-ZD-01", "tutar": "42500.00",
                                                     "islAmc": "06", "aciklama": "EFT - PERA YAYINCILIK AS - fatura"})
    assert r.status_code == 201
    tick()
    assert any(e["olayTipi"] == "KAYNAK_GUNCELLENDI" and e["kaynakTipi"] == "BAKIYE"
               and e["kaynakNo"] == "A9002-ZD-01" for e in sink)
    after = client.get("/hesaplar/A9002-ZD-01/bakiye", headers=h(**{"X-Access-Token": tok, "PSU-Initiated": "E"})).json()
    from decimal import Decimal as D
    assert D(after["bky"]["bkyTtr"]) - D(before["bky"]["bkyTtr"]) == D("42500.00")

    # 5) banka tarafında iptal (02) → KAYNAK_GUNCELLENDI / HESAP_BILGISI_RIZASI
    client.post(f"/_mock/simulasyon/riza-iptal/{riza}")
    tick()
    assert any(e["olayTipi"] == "KAYNAK_GUNCELLENDI" and e["kaynakTipi"] == "HESAP_BILGISI_RIZASI"
               and e["kaynakNo"] == riza for e in sink)
    client.delete(f"/olay-abonelik/{sub_no}", headers=h())


def test_ayrik_reject_sends_failure(client, sink):
    sub = client.post("/olay-abonelik", json={"katilimciBlg": {"hhsKod": HHS, "yosKod": "8001"},
                                              "abonelikTipleri": ALL_TYPES}, headers=h()).json()["olayAbonelikNo"]
    riza = client.post("/hesap-bilgisi-rizasi", json=ayrik_body(), headers=h()).json()["rzBlg"]["rizaNo"]
    approve_in_bank_app(client, riza, action="reddet")
    tick()
    assert any(e["olayTipi"] == "AYRIK_GKD_BASARISIZ" and e["kaynakNo"] == riza for e in sink)
    rz = client.get(f"/hesap-bilgisi-rizasi/{riza}", headers=h()).json()["rzBlg"]
    assert rz["rizaDrm"] == "I" and rz["rizaIptDtyKod"] == "13"
    client.delete(f"/olay-abonelik/{sub}", headers=h())


def test_undelivered_after_3_attempts(client, monkeypatch):
    async def down(url, body):
        raise ConnectionError("dinleyici kapalı")

    monkeypatch.setattr(events, "sender", down)
    monkeypatch.setattr(config, "EVENT_RETRY_SCALE", 0.0)
    sub = client.post("/olay-abonelik", json={"katilimciBlg": {"hhsKod": HHS, "yosKod": "8001"},
                                              "abonelikTipleri": ALL_TYPES}, headers=h()).json()["olayAbonelikNo"]
    riza = client.post("/hesap-bilgisi-rizasi", json=ayrik_body(), headers=h()).json()["rzBlg"]["rizaNo"]
    approve_in_bank_app(client, riza, action="reddet")
    for _ in range(3):
        tick()
    r = client.get(f"/olay-abonelik/{sub}/iletilemeyen-olaylar", headers=h())
    assert r.status_code == 200 and any(e["kaynakNo"] == riza for e in r.json())
    client.delete(f"/olay-abonelik/{sub}", headers=h())


def test_bank_app_shows_only_latest_request(client):
    sub = client.post("/olay-abonelik", json={"katilimciBlg": {"hhsKod": HHS, "yosKod": "8001"},
                                              "abonelikTipleri": ALL_TYPES}, headers=h()).json()["olayAbonelikNo"]
    first = client.post("/hesap-bilgisi-rizasi", json=ayrik_body(), headers=h()).json()["rzBlg"]["rizaNo"]
    second = client.post("/hesap-bilgisi-rizasi", json=ayrik_body(), headers=h()).json()["rzBlg"]["rizaNo"]
    r = client.post("/banka-uygulamasi/giris", data={"hhsKod": HHS, "tckn": ZEYNEP, "sifre": "Test1234"})
    page = client.get(r.headers["location"]).text
    assert second in page and first not in page
    assert page.count('name="rizaNo"') == 1
    client.delete(f"/olay-abonelik/{sub}", headers=h())
