# mock-hhs

TCMB ÖHVPS v2.0.0 **Hesap Bilgisi Hizmeti** uçlarını taklit eden sahte banka (HHS) sunucusu. Bankalar ve kullanıcılar kurgusaldır.

## Çalıştırma

```bash
cd mock-hhs
python -m venv .venv
# Windows: .venv\Scripts\activate   |   Mac/Linux: source .venv/bin/activate
pip install -r requirements.txt
uvicorn app.main:app --reload --port 8090
```

- Swagger: http://localhost:8090/docs
- Test kullanıcıları ve kodlar: http://localhost:8090/_mock/info

Docker ile:

```bash
docker build -t fincopilot-mock-hhs .
docker run -p 8090:8090 -v mockhhs:/data fincopilot-mock-hhs
```

Testler:

```bash
pytest
```

## Durum

| Parça | Durum |
|---|---|
| `POST/GET/DELETE /hesap-bilgisi-rizasi` | ✅ |
| Sahte banka GKD sayfası (yönlendirmeli) | ✅ |
| `POST /erisim-belirteci` (yet_kod + yenileme_belirteci) | ✅ |
| Rıza durum makinesi, süre aşımları, iptal kodları 01/03/04/05/08/09/13 | ✅ |
| Hesap, bakiye, işlem, kart, kart detay, kart hareketleri uçları | ✅ |
| İzin kontrolü (01–09), seçili hesap/kart filtresi, PSU-Initiated pencere kuralları, günde 4 sistem sorgusu | ✅ |
| Sentetik veri üretici (3 persona × 12 ay, BDDK/TCMB parametreleriyle) | ✅ |
| Ayrık GKD + olay bildirimi | ⏳ Faz 1 – adım 3 |

## Sentetik veri

Veri `app/generator/` altında günlük simülasyonla üretilir: son 365 gün, her gün için deterministik.
Faiz, asgari ödeme, limit tavanı ve vergiler BDDK/TCMB parametrelerinden gelir
(bkz. [docs/mock-veri-parametreleri.md](../docs/mock-veri-parametreleri.md)).

| Persona | Profil | Gösterdiği senaryolar |
|---|---|---|
| Ayşe Yılmaz (10000000146) | 22, öğrenci, Manisa. Kafe maaşı + aile harçlığı + burs, yurt | Taksitli yenilenmiş telefon (6 taksit), 3 gün gecikmeli ödeme, sadece asgari ödeme → faiz, abonelik zammı, ay sonu sıkışıklığı |
| Mehmet Kaya (10000000214) | 34, yazılımcı, İstanbul. Maaş (ayın 15'i, `islAmc=05`), kira (`01`), aidat (`11`) | İki kart arasında 15 gün kesim farkı, KMH kullanırken diğer bankada atıl para, 12 taksitli TV, EFT ücreti, hesap işletim ücreti, %40 asgari ödeme (limit > 100.000) |
| Zeynep Demir (10000000382) | 29, serbest tasarımcı, İzmir. Düzensiz müşteri ödemeleri, Bağ-Kur, geçici vergi | Kodsuz kira (örüntüyle bulunmalı), USD abonelikler + Adobe zammı, Amsterdam'da EUR harcama, mükerrer çekim, aylarca atıl kalan yüksek bakiye, KMH |

Tüm profil, hesap, kart, limit ve hesap kesim günleri: `GET /_mock/info`

## Test verisi

| hhsKod | Banka |
|---|---|
| 9001 | Anadolu Bank |
| 9002 | Ege Bank |
| 9003 | Marmara Bank |

| TCKN | Ad | Şifre |
|---|---|---|
| 10000000146 | Ayşe Yılmaz | Test1234 |
| 10000000214 | Mehmet Kaya | Test1234 |
| 10000000382 | Zeynep Demir | Test1234 |

YÖS kodu (FinCopilot): `8001` · SMS kodu: `123456`

## Akış

1. YÖS → `POST /hesap-bilgisi-rizasi` (başlıklar: `X-Request-ID`, `X-ASPSP-Code`, `X-TPP-Code`). Yanıt: `rizaNo`, `rizaDrm=B`, `gkd.hhsYonAdr`.
2. Kullanıcı `hhsYonAdr?drmKod=<YÖS'ün ürettiği değer>` adresine yönlendirilir.
3. Sahte banka: TCKN + şifre → SMS kodu → hesap/kart seçimi → Onayla.
4. Banka `yonAdr?rizaNo=...&yetKod=...&drmKod=...` adresine döner (`rizaDrm=Y`).
   Hata/vazgeçme durumunda: `yonAdr?rizaNo=...&rizaDrm=I&rizaIptDtyKod=..&drmKod=...`
5. YÖS → `POST /erisim-belirteci` `{rizaNo, rizaTip:"H", yetTip:"yet_kod", yetKod}` → `rizaDrm=K`.

## Spesifikasyondan doğrulanması gerekenler

- İşlem detayı (`islDty`), kart listesi, kart detay ve kart hareketleri alan adları. Hepsi `app/generator/store.py` içindeki dönüştürücülerde tek yerde.
- Sayfalama ve sıralama parametrelerinin değerleri (`srlmYon`), toplam kayıt bilgisinin nasıl döndüğü (mock: `X-Total-Count` başlığı).
- `kanal` kod değerleri.
- Uçların gerçek base path'i (ÖHVPS sürümlü bir ön ek kullanıyor olabilir). Mock şimdilik kökten sunuyor; YÖS tarafında base URL ayardan geliyor.
- Hata yanıtı gövdesinin alan adları (`errors.py`).
- GKD geri dönüşündeki parametre adları ve `drmKod`'un taşınma şekli.
- JWS imzası (`X-JWS-Signature`) mock'ta doğrulanmıyor.
