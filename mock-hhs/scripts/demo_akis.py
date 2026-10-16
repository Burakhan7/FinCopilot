"""Uçtan uca demo: rıza → sahte banka onayı (tarayıcıda) → token → veri çekme.

Kullanım (sunucu çalışırken, ayrı bir terminalde):
    python scripts/demo_akis.py                      # Mehmet, Anadolu Bank
    python scripts/demo_akis.py --tckn 10000000146   # Ayşe
    python scripts/demo_akis.py --tckn 10000000382 --banka 9002
"""
import argparse
import secrets
import sys
import time
import uuid
import webbrowser
from datetime import datetime, timedelta, timezone

import httpx

TR = timezone(timedelta(hours=3))
YOS = "8001"


def h(banka, token=None, psu=None):
    out = {"X-Request-ID": str(uuid.uuid4()), "X-ASPSP-Code": banka, "X-TPP-Code": YOS}
    if token:
        out["X-Access-Token"] = token
    if psu:
        out["PSU-Initiated"] = psu
    return out


def fail(r):
    print(f"\n❌ {r.request.method} {r.request.url.path} → {r.status_code}\n{r.text}")
    sys.exit(1)


def tl(x):
    return f"{float(x):>14,.2f}".replace(",", "X").replace(".", ",").replace("X", ".")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", default="http://localhost:8090")
    ap.add_argument("--tckn", default="10000000214")
    ap.add_argument("--banka", default="9001")
    a = ap.parse_args()
    c = httpx.Client(base_url=a.base, timeout=30)

    # 0. Sunucu ayakta mı?
    try:
        c.get("/_mock/health").raise_for_status()
    except Exception:
        print(f"❌ Sunucuya ulaşılamadı: {a.base}. Önce 'uvicorn app.main:app --port 8090' çalıştır.")
        sys.exit(1)

    # 1. Rıza oluştur
    now = datetime.now(TR)
    body = {
        "katilimciBlg": {"hhsKod": a.banka, "yosKod": YOS},
        "gkd": {"yetYntm": "Y", "yonAdr": f"{a.base}/_mock/callback"},
        "kmlk": {"kmlkTur": "K", "kmlkVrs": a.tckn, "ohkTur": "B"},
        "hspBlg": {"iznBlg": {
            "iznTur": ["01", "02", "03", "04", "05", "07", "08", "09"],
            "erisimIzniSonTrh": (now + timedelta(days=180)).isoformat(),
            "hesapIslemBslZmn": (now - timedelta(days=365)).isoformat(),
            "hesapIslemBtsZmn": (now + timedelta(days=180)).isoformat(),
        }},
    }
    r = c.post("/hesap-bilgisi-rizasi", json=body, headers=h(a.banka))
    if r.status_code != 201:
        fail(r)
    rz = r.json()
    riza = rz["rzBlg"]["rizaNo"]
    drm = secrets.token_urlsafe(12)
    url = f"{rz['gkd']['hhsYonAdr']}?drmKod={drm}"
    print(f"1) Rıza oluşturuldu: {riza}  (durum: {rz['rzBlg']['rizaDrm']})")

    # 2. Kullanıcı bankada onaylar
    print("2) Sahte banka sayfası açılıyor. Açılmazsa bu adresi tarayıcıya yapıştır:")
    print(f"   {url}")
    print(f"   Giriş: TCKN {a.tckn} · şifre Test1234 · SMS 123456 → hesap/kart seç → Onayla")
    webbrowser.open(url)
    cb = None
    for _ in range(300):  # 5 dakika
        r = c.get(f"/_mock/callback/{riza}")
        if r.status_code == 200:
            cb = r.json()
            break
        time.sleep(1)
    if cb is None:
        print("❌ 5 dakika içinde onay gelmedi.")
        sys.exit(1)
    if "yetKod" not in cb:
        print(f"❌ Rıza iptal edildi: rizaIptDtyKod={cb.get('rizaIptDtyKod')}")
        sys.exit(1)
    if cb.get("drmKod") != drm:
        print("❌ drmKod eşleşmedi (CSRF kontrolü)")
        sys.exit(1)
    print(f"   ✅ Onay geldi, drmKod doğrulandı. yetKod: {cb['yetKod'][:10]}...")

    # 3. Token
    r = c.post("/erisim-belirteci", headers=h(a.banka),
               json={"rizaNo": riza, "rizaTip": "H", "yetTip": "yet_kod", "yetKod": cb["yetKod"]})
    if r.status_code != 200:
        fail(r)
    tok = r.json()["erisimBelirteci"]
    durum = c.get(f"/hesap-bilgisi-rizasi/{riza}", headers=h(a.banka)).json()["rzBlg"]["rizaDrm"]
    print(f"3) Erişim belirteci alındı. Rıza durumu: {durum}")

    # 4. Veri
    print("\n4) VERİLER")
    r = c.get("/hesaplar", headers=h(a.banka, tok, "E"))
    if r.status_code != 200:
        fail(r)
    hesaplar = r.json()
    bakiyeler = {b["hspRef"]: b for b in c.get("/bakiye", headers=h(a.banka, tok, "E")).json()}
    for hs in hesaplar:
        t = hs["hspTml"]
        b = bakiyeler[t["hspRef"]]
        kmh = f"  (KMH kullanılan: {b['krdHsp']['kulKrdTtr']})" if "krdHsp" in b else ""
        print(f"   🏦 {t['hspUrunAdi']:<16} {t['hspNo']}  bakiye:{tl(b['bky']['bkyTtr'])} {t['prBrm']}{kmh}")
        w = {"hesapIslemBslTrh": (now - timedelta(days=30)).isoformat(), "hesapIslemBtsTrh": now.isoformat(),
             "syfKytSayi": 5}
        r = c.get(f"/hesaplar/{t['hspRef']}/islemler", headers=h(a.banka, tok, "E"), params=w)
        if r.status_code != 200:
            fail(r)
        print(f"      son 30 gün: {r.headers['X-Total-Count']} işlem, en yeni 5:")
        for i in r.json()["isller"]:
            x = i["islTml"]
            sign = "+" if x["brcAlc"] == "A" else "-"
            print(f"      {x['islGrckZaman'][:10]} {sign}{tl(x['islTtr'])}  {x['islTur']:<22} {x.get('islAcklm', '')[:45]}")

    for k in c.get("/kartlar", headers=h(a.banka, tok, "E")).json():
        print(f"   💳 {k['kartUrunAdi']} ****{k['kartSonNo']} ({'kredi' if k['kartTipi'] == 'K' else 'banka'} kartı)")
        if k["kartTipi"] != "K":
            continue
        d = c.get(f"/kartlar/{k['kartRef']}/kart-detay", headers=h(a.banka, tok, "E")).json()
        print(f"      limit:{tl(d['limitTutari'])}  kullanılan:{tl(d['kullanilanLimit'])}")
        print(f"      son ekstre: kesim {d['hesapKesimTarihi'][:10]} · son ödeme {d['sonOdemeTarihi'][:10]}"
              f" · borç {tl(d['donemBorcu']).strip()} · asgari {tl(d['asgariOdeme']).strip()}")
        w = {"hesapIslemBslTrh": (now - timedelta(days=30)).isoformat(), "hesapIslemBtsTrh": now.isoformat(),
             "syfKytSayi": 5}
        r = c.get(f"/kartlar/{k['kartRef']}/kart-hareketleri", headers=h(a.banka, tok, "E"), params=w)
        print(f"      son 30 gün: {r.headers['X-Total-Count']} hareket, en yeni 5:")
        for m in r.json()["kartHareketleri"]:
            ek = f" [{m['taksitSayisi']} taksit]" if m.get("taksitSayisi") else ""
            fx = f" ({m['dovizBilgisi']['orijinalTutar']} {m['dovizBilgisi']['orijinalParaBirimi']})" if m.get("dovizBilgisi") else ""
            print(f"      {m['islemTarihi'][:10]} {tl(m['islemTutari'])}  {m['islemAciklama'][:40]}{fx}{ek}")

    # 5. Kurallar
    print("\n5) KURAL KONTROLLERİ")
    ref = hesaplar[0]["hspTml"]["hspRef"]
    w40 = {"hesapIslemBslTrh": (now - timedelta(days=40)).isoformat(), "hesapIslemBtsTrh": now.isoformat()}
    r = c.get(f"/hesaplar/{ref}/islemler", headers=h(a.banka, tok, "E"), params=w40)
    print(f"   40 günlük sorgu (sınır 1 ay)  → {r.status_code} {r.json().get('errorCode', '')}")
    w1 = {"hesapIslemBslTrh": (now - timedelta(hours=20)).isoformat(), "hesapIslemBtsTrh": now.isoformat()}
    codes = [c.get(f"/hesaplar/{ref}/islemler", headers=h(a.banka, tok, "H"), params=w1).status_code for _ in range(5)]
    print(f"   5 sistem sorgusu (günde 4)    → {codes}")
    r = c.delete(f"/hesap-bilgisi-rizasi/{riza}", headers={**h(a.banka), "X-Access-Token": tok})
    rz = c.get(f"/hesap-bilgisi-rizasi/{riza}", headers=h(a.banka)).json()["rzBlg"]
    print(f"   Rıza iptali                   → {r.status_code}, durum {rz['rizaDrm']}, kod {rz.get('rizaIptDtyKod')}")
    r = c.get("/hesaplar", headers=h(a.banka, tok, "E"))
    print(f"   İptalden sonra veri isteği    → {r.status_code} {r.json().get('errorCode', '')}")
    print("\n✅ Demo tamamlandı.")


if __name__ == "__main__":
    main()
