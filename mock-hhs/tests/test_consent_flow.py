import os
import re
import tempfile
import uuid
from datetime import datetime, timedelta, timezone
from urllib.parse import parse_qs, urlparse

_db = os.path.join(tempfile.mkdtemp(), "test.db")
os.environ["MOCK_HHS_DATABASE_URL"] = f"sqlite:///{_db}"
os.environ["MOCK_HHS_EVENT_LOOP"] = "0"  # testlerde olay döngüsünü elle çalıştırıyoruz

import pytest  # noqa: E402
from fastapi.testclient import TestClient  # noqa: E402

from app.main import app  # noqa: E402

TR = timezone(timedelta(hours=3))
TCKN = "10000000214"  # Mehmet Kaya
HHS = "9001"
YOS = "8001"
CALLBACK = "fincopilot://gkd/callback"


@pytest.fixture(scope="module")
def client():
    with TestClient(app, follow_redirects=False) as c:
        yield c


def headers(hhs=HHS, yos=YOS, **extra):
    return {"X-Request-ID": str(uuid.uuid4()), "X-ASPSP-Code": hhs, "X-TPP-Code": yos, **extra}


def consent_body(**over):
    now = datetime.now(TR)
    body = {
        "katilimciBlg": {"hhsKod": HHS, "yosKod": YOS},
        "gkd": {"yetYntm": "Y", "yonAdr": CALLBACK},
        "kmlk": {"kmlkTur": "K", "kmlkVrs": TCKN, "ohkTur": "B"},
        "hspBlg": {"iznBlg": {
            "iznTur": ["01", "02", "03", "04", "05", "07", "08", "09"],
            "erisimIzniSonTrh": (now + timedelta(days=180)).isoformat(),
            "hesapIslemBslZmn": (now - timedelta(days=365)).isoformat(),
            "hesapIslemBtsZmn": (now + timedelta(days=180)).isoformat(),
        }},
    }
    for k, v in over.items():
        body[k] = v
    return body


def create(client, body=None, **h):
    return client.post("/hesap-bilgisi-rizasi", json=body or consent_body(), headers=headers(**h))


def run_gkd(client, riza_no, tckn=TCKN, drm="drm-123", action="onay"):
    """Sahte banka sayfasını uçtan uca doldurur, geri dönüş URL'sinin parametrelerini döner."""
    r = client.get(f"/gkd/{riza_no}", params={"drmKod": drm})
    assert r.status_code == 200
    sid = re.search(r'name="sid" value="([^"]+)"', r.text).group(1)
    r = client.post(f"/gkd/{riza_no}/giris", data={"sid": sid, "tckn": tckn, "sifre": "Test1234"})
    if r.status_code == 303:
        return parse_qs(urlparse(r.headers["location"]).query)
    assert r.status_code == 200 and "SMS" in r.text
    r = client.post(f"/gkd/{riza_no}/otp", data={"sid": sid, "otp": "123456"})
    if r.status_code == 303:
        return parse_qs(urlparse(r.headers["location"]).query)
    assert r.status_code == 200
    refs = re.findall(r'name="(hspRef|kartRef)" value="([^"]+)"', r.text)
    data = {"sid": sid, "islem": action, "hspRef": [v for k, v in refs if k == "hspRef"],
            "kartRef": [v for k, v in refs if k == "kartRef"]}
    r = client.post(f"/gkd/{riza_no}/onay", data=data)
    assert r.status_code == 303
    assert r.headers["location"].startswith(CALLBACK)
    return parse_qs(urlparse(r.headers["location"]).query)


def get_token(client, riza_no, yet_kod):
    return client.post("/erisim-belirteci", headers=headers(),
                       json={"rizaNo": riza_no, "rizaTip": "H", "yetTip": "yet_kod", "yetKod": yet_kod})


# ---------------- Mutlu yol ----------------
def test_full_flow_create_gkd_token_revoke(client):
    r = create(client)
    assert r.status_code == 201, r.text
    body = r.json()
    riza_no = body["rzBlg"]["rizaNo"]
    assert body["rzBlg"]["rizaDrm"] == "B"
    assert body["gkd"]["hhsYonAdr"].endswith(f"/gkd/{riza_no}")
    assert r.headers["X-Request-ID"]

    q = run_gkd(client, riza_no)
    assert q["rizaNo"] == [riza_no] and q["drmKod"] == ["drm-123"] and "yetKod" in q

    r = client.get(f"/hesap-bilgisi-rizasi/{riza_no}", headers=headers())
    assert r.json()["rzBlg"]["rizaDrm"] == "Y"

    r = get_token(client, riza_no, q["yetKod"][0])
    assert r.status_code == 200, r.text
    tok = r.json()
    assert tok["gecerlilikSuresi"] > 0 and tok["yenilemeBelirteciGecerlilikSuresi"] > tok["gecerlilikSuresi"]

    r = client.get(f"/hesap-bilgisi-rizasi/{riza_no}", headers=headers())
    assert r.json()["rzBlg"]["rizaDrm"] == "K"

    # yetKod tek kullanımlık
    assert get_token(client, riza_no, q["yetKod"][0]).status_code == 401

    # yenileme: yenilemeBelirteci değişmez
    r = client.post("/erisim-belirteci", headers=headers(), json={
        "rizaNo": riza_no, "rizaTip": "H", "yetTip": "yenileme_belirteci",
        "yenilemeBelirteci": tok["yenilemeBelirteci"]})
    assert r.status_code == 200
    assert r.json()["yenilemeBelirteci"] == tok["yenilemeBelirteci"]
    assert r.json()["erisimBelirteci"] != tok["erisimBelirteci"]

    # K durumunda iptal için erişim belirteci gerekir
    assert client.delete(f"/hesap-bilgisi-rizasi/{riza_no}", headers=headers()).status_code == 401
    r = client.delete(f"/hesap-bilgisi-rizasi/{riza_no}", headers=headers(**{"X-Access-Token": tok["erisimBelirteci"]}))
    assert r.status_code == 204
    rz = client.get(f"/hesap-bilgisi-rizasi/{riza_no}", headers=headers()).json()["rzBlg"]
    assert rz["rizaDrm"] == "I" and rz["rizaIptDtyKod"] == "03"


def test_new_consent_cancels_previous_with_code_01(client):
    first = create(client).json()["rzBlg"]["rizaNo"]
    q = run_gkd(client, first)
    get_token(client, first, q["yetKod"][0])

    second = create(client).json()["rzBlg"]["rizaNo"]
    run_gkd(client, second)

    rz = client.get(f"/hesap-bilgisi-rizasi/{first}", headers=headers()).json()["rzBlg"]
    assert rz["rizaDrm"] == "I" and rz["rizaIptDtyKod"] == "01"


# ---------------- GKD iptal senaryoları ----------------
def test_gkd_tckn_mismatch_cancels_08(client):
    riza_no = create(client).json()["rzBlg"]["rizaNo"]
    q = run_gkd(client, riza_no, tckn="10000000146")  # başka kullanıcı
    assert q["rizaDrm"] == ["I"] and q["rizaIptDtyKod"] == ["08"]


def test_gkd_user_cancels_13(client):
    riza_no = create(client).json()["rzBlg"]["rizaNo"]
    q = run_gkd(client, riza_no, action="vazgec")
    assert q["rizaDrm"] == ["I"] and q["rizaIptDtyKod"] == ["13"]


def test_gkd_no_products_cancels_09(client):
    body = consent_body(katilimciBlg={"hhsKod": "9002", "yosKod": YOS})  # Mehmet'in 9002'de ürünü yok
    riza_no = create(client, body, hhs="9002").json()["rzBlg"]["rizaNo"]
    q = run_gkd(client, riza_no)
    assert q["rizaDrm"] == ["I"] and q["rizaIptDtyKod"] == ["09"]


def test_wrong_password_stays_on_login(client):
    riza_no = create(client).json()["rzBlg"]["rizaNo"]
    r = client.get(f"/gkd/{riza_no}")
    sid = re.search(r'name="sid" value="([^"]+)"', r.text).group(1)
    r = client.post(f"/gkd/{riza_no}/giris", data={"sid": sid, "tckn": TCKN, "sifre": "yanlis"})
    assert r.status_code == 401 and "hatalı" in r.text


# ---------------- Doğrulama ----------------
@pytest.mark.parametrize("izin, beklenen", [
    (["02", "03"], "IncorrectPermissionType"),      # 01/07 yok
    (["01", "05"], "IncorrectPermissionType"),      # 05 için 04 yok
    (["01", "06"], "IncorrectPermissionType"),      # 06 için 03 yok
    (["01", "99"], "IncorrectPermissionType"),      # bilinmeyen kod
])
def test_permission_rules(client, izin, beklenen):
    body = consent_body()
    body["hspBlg"]["iznBlg"]["iznTur"] = izin
    r = create(client, body)
    assert r.status_code == 400 and beklenen in r.json()["errorCode"]


def test_consent_longer_than_6_months_rejected(client):
    body = consent_body()
    body["hspBlg"]["iznBlg"]["erisimIzniSonTrh"] = (datetime.now(TR) + timedelta(days=200)).isoformat()
    r = create(client, body)
    assert r.status_code == 400 and r.json()["errorCode"].endswith("InvalidStartEndTime")


def test_header_body_mismatch(client):
    assert create(client, hhs="9002").json()["errorCode"].endswith("InvalidASPSP")
    assert create(client, yos="1234").json()["errorCode"].endswith("InvalidTPP")


def test_missing_headers_rejected(client):
    r = client.post("/hesap-bilgisi-rizasi", json=consent_body())
    assert r.status_code == 400 and r.json()["errorCode"] == "TR.OHVPS.Field.Invalid"


def test_other_bank_cannot_read_consent(client):
    riza_no = create(client).json()["rzBlg"]["rizaNo"]
    r = client.get(f"/hesap-bilgisi-rizasi/{riza_no}", headers=headers(hhs="9002"))
    assert r.status_code == 400


def test_dev_callback_page_stores_result(client):
    body = consent_body()
    body["gkd"]["yonAdr"] = "http://testserver/_mock/callback"
    riza = create(client, body).json()["rzBlg"]["rizaNo"]
    assert client.get(f"/_mock/callback/{riza}").status_code == 404
    q = run_gkd_any(client, riza)
    r = client.get(q)
    assert r.status_code == 200 and "Onay alındı" in r.text
    assert "yetKod" in client.get(f"/_mock/callback/{riza}").json()


def run_gkd_any(client, riza_no):
    r = client.get(f"/gkd/{riza_no}", params={"drmKod": "d"})
    sid = re.search(r'name="sid" value="([^"]+)"', r.text).group(1)
    client.post(f"/gkd/{riza_no}/giris", data={"sid": sid, "tckn": TCKN, "sifre": "Test1234"})
    r = client.post(f"/gkd/{riza_no}/otp", data={"sid": sid, "otp": "123456"})
    refs = re.findall(r'name="(hspRef|kartRef)" value="([^"]+)"', r.text)
    r = client.post(f"/gkd/{riza_no}/onay", data={"sid": sid, "islem": "onay",
                                                  "hspRef": [v for k, v in refs if k == "hspRef"]})
    return r.headers["location"].replace("http://testserver", "")


def test_new_request_cancels_pending_one_with_01(client):
    first = create(client).json()["rzBlg"]["rizaNo"]
    second = create(client).json()["rzBlg"]["rizaNo"]
    rz = client.get(f"/hesap-bilgisi-rizasi/{first}", headers=headers()).json()["rzBlg"]
    assert rz["rizaDrm"] == "I" and rz["rizaIptDtyKod"] == "01"
    assert client.get(f"/hesap-bilgisi-rizasi/{second}", headers=headers()).json()["rzBlg"]["rizaDrm"] == "B"
    # eski isteğin banka sayfası artık açılmaz
    assert client.get(f"/gkd/{first}").status_code == 400
