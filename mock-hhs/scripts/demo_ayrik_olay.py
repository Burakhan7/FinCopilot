"""Ayrık GKD + olay bildirimi demosu.

Akış: olay aboneliği → ayrık rıza → kullanıcı "banka mobil uygulamasında" onaylar
→ AYRIK_GKD_BASARILI olayı → yetKod → token → "maaş yattı" simülasyonu → BAKIYE olayı
→ banka tarafında iptal → rıza olayı.

Kullanım (sunucu çalışırken, ikinci terminalde):
    python scripts/demo_ayrik_olay.py                                     # Zeynep · Ege Bank
    python scripts/demo_ayrik_olay.py --tckn 10000000214 --banka 9003 --hesap A9003-MK-01
"""
import argparse
import sys
import time
import uuid
import webbrowser
from datetime import datetime, timedelta, timezone

import httpx

TR = timezone(timedelta(hours=3))
YOS = "8001"
TIPLER = [
    {"olayTipi": "AYRIK_GKD_BASARILI", "kaynakTipi": "HESAP_BILGISI_RIZASI"},
    {"olayTipi": "AYRIK_GKD_BASARISIZ", "kaynakTipi": "HESAP_BILGISI_RIZASI"},
    {"olayTipi": "KAYNAK_GUNCELLENDI", "kaynakTipi": "HESAP_BILGISI_RIZASI"},
    {"olayTipi": "KAYNAK_GUNCELLENDI", "kaynakTipi": "BAKIYE"},
]


def h(banka, **extra):
    return {"X-Request-ID": str(uuid.uuid4()), "X-ASPSP-Code": banka, "X-TPP-Code": YOS, **extra}


def fail(r):
    print(f"\n❌ {r.request.method} {r.request.url.path} → {r.status_code}\n{r.text}")
    sys.exit(1)


def wait_event(c, kaynak_no, olay_tipi, seen, timeout=300):
    for _ in range(timeout):
        for e in c.get("/_mock/yos/olaylar", params={"kaynakNo": kaynak_no}).json():
            if e["olayTipi"] in olay_tipi and e["olayNo"] not in seen:
                seen.add(e["olayNo"])
                return e
        time.sleep(1)
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", default="http://localhost:8090")
    ap.add_argument("--tckn", default="10000000382")
    ap.add_argument("--banka", default="9002")
    ap.add_argument("--hesap", default="A9002-ZD-01", help="maaş/ödeme simülasyonu yapılacak hesap")
    a = ap.parse_args()
    c = httpx.Client(base_url=a.base, timeout=30)
    seen: set[str] = set()
    try:
        c.get("/_mock/health").raise_for_status()
    except Exception:
        print(f"❌ Sunucuya ulaşılamadı: {a.base}")
        sys.exit(1)

    # 1. Olay aboneliği (varsa güncelle)
    body = {"katilimciBlg": {"hhsKod": a.banka, "yosKod": YOS}, "abonelikTipleri": TIPLER}
    r = c.get("/olay-abonelik", headers=h(a.banka))
    if r.status_code == 200:
        r = c.put(f"/olay-abonelik/{r.json()['olayAbonelikNo']}", json=body, headers=h(a.banka))
    else:
        r = c.post("/olay-abonelik", json=body, headers=h(a.banka))
    if r.status_code not in (200, 201):
        fail(r)
    print(f"1) Olay aboneliği hazır: {r.json()['olayAbonelikNo']}")

    # 2. Ayrık GKD rızası
    now = datetime.now(TR)
    body = {
        "katilimciBlg": {"hhsKod": a.banka, "yosKod": YOS},
        "gkd": {"yetYntm": "A", "yonAdr": "fincopilot://gkd",
                "ayrikGkd": {"ohkTanimTip": "TCKN", "ohkTanimDeger": a.tckn}},
        "kmlk": {"kmlkTur": "K", "kmlkVrs": a.tckn, "ohkTur": "B"},
        "hspBlg": {"iznBlg": {
            "iznTur": ["01", "02", "03", "04", "05", "06", "07", "08", "09"],
            "erisimIzniSonTrh": (now + timedelta(days=180)).isoformat(),
            "hesapIslemBslZmn": (now - timedelta(days=365)).isoformat(),
            "hesapIslemBtsZmn": (now + timedelta(days=180)).isoformat(),
        }},
    }
    r = c.post("/hesap-bilgisi-rizasi", json=body, headers=h(a.banka))
    if r.status_code != 201:
        fail(r)
    riza = r.json()["rzBlg"]["rizaNo"]
    print(f"2) Ayrık rıza oluşturuldu: {riza} (durum B). Kullanıcı FinCopilot'tan çıkmıyor.")

    url = f"{a.base}/banka-uygulamasi?hhsKod={a.banka}"
    print("3) Banka 'mobil uygulamasına' bildirim gitti. Açılmazsa bu adresi tarayıcıya yapıştır:")
    print(f"   {url}")
    print(f"   Giriş: TCKN {a.tckn} · şifre Test1234 → bekleyen isteği Onayla (5 dk içinde)")
    webbrowser.open(url)

    e = wait_event(c, riza, ("AYRIK_GKD_BASARILI", "AYRIK_GKD_BASARISIZ"), seen)
    if e is None:
        print("❌ Olay gelmedi.")
        sys.exit(1)
    print(f"   📩 Olay: {e['olayTipi']} ({e['olayZamani']})")
    if e["olayTipi"] != "AYRIK_GKD_BASARILI":
        rz = c.get(f"/hesap-bilgisi-rizasi/{riza}", headers=h(a.banka)).json()["rzBlg"]
        print(f"❌ Onay verilmedi: kod {rz.get('rizaIptDtyKod')}")
        sys.exit(1)

    # 4. yetKod → token
    rz = c.get(f"/hesap-bilgisi-rizasi/{riza}", headers=h(a.banka)).json()
    r = c.post("/erisim-belirteci", headers=h(a.banka),
               json={"rizaNo": riza, "rizaTip": "H", "yetTip": "yet_kod", "yetKod": rz["gkd"]["yetKod"]})
    if r.status_code != 200:
        fail(r)
    tok = r.json()["erisimBelirteci"]
    print("4) yetKod rıza sorgusundan alındı, token alındı (durum K).")

    # 5. Maaş yattı → BAKIYE olayı
    dh = h(a.banka, **{"X-Access-Token": tok, "PSU-Initiated": "E"})
    once = c.get(f"/hesaplar/{a.hesap}/bakiye", headers=dh).json()["bky"]["bkyTtr"]
    c.post("/_mock/simulasyon/olay-tick")  # tarama başlangıcını kaydet
    time.sleep(1)
    c.post("/_mock/simulasyon/islem", json={"tckn": a.tckn, "hspRef": a.hesap, "tutar": "35000.00",
                                            "islAmc": "06", "aciklama": "EFT - ATLAS REKLAM AJANSI LTD STI - fatura"})
    print("5) Hesaba 35.000 TL gelen havale simüle edildi, banka bildirimi bekleniyor...")
    e = wait_event(c, a.hesap, ("KAYNAK_GUNCELLENDI",), seen, timeout=60)
    if e is None:
        print("❌ BAKIYE olayı gelmedi.")
        sys.exit(1)
    sonra = c.get(f"/hesaplar/{a.hesap}/bakiye", headers=dh).json()["bky"]["bkyTtr"]
    print(f"   📩 Olay: {e['olayTipi']} / {e['kaynakTipi']} / {e['kaynakNo']}")
    print(f"   Bakiye: {once} → {sonra}")

    # 6. Banka tarafında iptal
    c.post(f"/_mock/simulasyon/riza-iptal/{riza}")
    print("6) Kullanıcı rızayı bankanın kendi uygulamasından iptal etti...")
    e = wait_event(c, riza, ("KAYNAK_GUNCELLENDI",), seen, timeout=60)
    rz = c.get(f"/hesap-bilgisi-rizasi/{riza}", headers=h(a.banka)).json()["rzBlg"]
    print(f"   📩 Olay: {e['olayTipi'] if e else '-'} / {e['kaynakTipi'] if e else '-'}"
          f" → rıza durumu {rz['rizaDrm']}, kod {rz.get('rizaIptDtyKod')}")
    print("\n✅ Demo tamamlandı. Alınan tüm olaylar: GET /_mock/yos/olaylar")


if __name__ == "__main__":
    main()
