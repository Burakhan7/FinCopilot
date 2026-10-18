# FinCopilot – Claude Code bağlamı

Bitirme projesi (Manisa Celal Bayar Üniv., teslim Haziran 2027). Geliştirici: Burakhan (GitHub: Burakhan7).
**Yapay zekâ destekli, bankadan bağımsız kişisel finans koçu.** TCMB ÖHVPS v2.0.0 Hesap Bilgisi Hizmeti
üzerinden birden çok bankadaki hesap/kart verisini toplar, anlamlandırır, proaktif öneri sunar.

> İlke: **LLM konuşur, backend hesaplar, kural motoru karar verir.** LLM sadece okuma yapan tool'ları çağırır.

## Çalışma kuralları
- **Teknoloji/mimari kararlarını kendin verme.** Seçenekleri (kısa artı/eksi + önerin) sun, Burakhan karar versin.
- Yanıtlar Türkçe, kısa ve doğrudan. Komutlar kopyala-yapıştır hazır (Windows cmd/PowerShell öncelikli).
- Her adım sonunda testleri çalıştır; geçmeden "bitti" deme.
- Spesifikasyondan doğrulanamayan şeyleri uydurma; README'deki "doğrulanması gerekenler" listesine ekle.
- Commit mesajları Türkçe, kısa.

## Alınan kararlar
| Konu | Karar |
|---|---|
| Mobil | Flutter + Riverpod |
| Ana API | .NET 8, Clean Architecture, CQRS/MediatR, EF Core |
| Veritabanı | Microsoft SQL Server 2022 |
| Arka plan işleri | Hangfire (SQL Server deposu) |
| Mock HHS | Python FastAPI (`mock-hhs/`, durumu SQLite'ta) |
| AI servisi | Python FastAPI (`ai-service/`) |
| Ana LLM | Gemini (function calling); karşılaştırma için Ollama yerel model |
| Banka kartı harcamaları | Sadece hesap hareketlerinden okunur (kart hareketlerinde tekrar yok) |

## Repo
```
docs/        mimari.md, veritabani.md (ER + tablolar), backend-fonksiyon-listesi.md,
             ohvps-hesap-bilgisi-referans.md, mock-veri-parametreleri.md, yol-plani.md
mock-hhs/    Sahte banka (bitti, 36 test)
api/         .NET backend (Faz 2 – henüz boş)
ai-service/  (Faz 3+ – boş)
mobile/      Flutter (Faz 2 sonu – boş)
```
Önce ilgili `docs/` dosyasını oku; tasarım orada.

## Durum (Ekim 2026)
**Faz 0 ✅** dokümanlar · **Faz 1 ✅** Mock HHS:
- Rıza (oluştur/sorgula/iptal), durum makinesi B→Y→K→S/I, iptal kodları (01,02,03,04,05,08,09,13)
- Yönlendirmeli GKD sahte banka sayfası (`/gkd/{rizaNo}`), ayrık GKD sahte banka mobil uygulaması (`/banka-uygulamasi`)
- Token (`/erisim-belirteci`, yet_kod + yenileme), veri uçları (hesaplar, bakiye, işlemler, kartlar, kart-detay, kart-hareketleri)
- İzin kontrolü 01–09, PSU-Initiated pencere kuralları (E: 1 ay, H: 24 saat), günde 4 sistem sorgusu
- Olay aboneliği + iletim (AYRIK_GKD_BASARILI/BASARISIZ, KAYNAK_GUNCELLENDI BAKIYE/HESAP_BILGISI_RIZASI), 3 deneme
- BDDK/TCMB parametreli 12 aylık sentetik veri, 3 persona (Ayşe öğrenci, Mehmet maaşlı, Zeynep serbest)
- Demo betikleri: `scripts/demo_akis.py`, `scripts/demo_ayrik_olay.py`

## Sıradaki: Faz 2 – .NET backend
1. Solution iskeleti (Domain/Application/Infrastructure/Api/Worker), SQL Server, ilk migration (`docs/veritabani.md`)
2. Kullanıcı kaydı/giriş
3. Rıza modülü (banka bağla, drmKod, durum takibi, token şifreleme)
4. `IHhsClient` adapter (MockHhsClient)
5. Senkronizasyon: ilk 12 ay yükleme, Hangfire ile günde ≤4 sorgu, `POST /olay-dinleme`
6. Uçtan uca test (Mock HHS + backend)

**Bekleyen kararlar (Burakhan'a sor):**
1. Giriş: kendi sistemimiz (telefon + SMS kodu + PIN + JWT) mı, Firebase Auth mu? (öneri: kendi sistemimiz)
2. Banka token şifreleme: AES-GCM (anahtar ortam değişkeninde) mi, ASP.NET Data Protection mı? (öneri: AES-GCM)
3. Mapping: elle / Mapster / AutoMapper (yeni sürümleri ücretli) (öneri: elle veya Mapster)
4. Doğrulama: FluentValidation mı, DataAnnotations mı? (öneri: FluentValidation)
5. Test: xUnit + Testcontainers (Docker gerekir) mı, bellek içi DB mi? (öneri: Testcontainers)
6. Bilgisayarda Docker var mı? Yoksa SQL Server Express/LocalDB.

## Mock HHS'yi çalıştırma
```
cd mock-hhs
.venv\Scripts\activate
pytest
uvicorn app.main:app --reload --port 8090      # Swagger: /docs · test kullanıcıları: /_mock/info
```
YÖS kodu `8001` · bankalar 9001 Anadolu, 9002 Ege, 9003 Marmara · şifre `Test1234` · SMS `123456`
Backend hazır olunca: `MOCK_HHS_YOS_EVENT_URL=http://localhost:<api-port>/olay-dinleme`

## ÖHVPS kritik kuralları (özet)
- Bir kullanıcının bir YÖS için bir bankada tek aktif rızası olur; yeni talep eskisini 01 ile iptal eder.
- Rıza bireyselde en fazla 6 ay. Access token `X-Access-Token` başlığıyla. Başlıklar: X-Request-ID, X-ASPSP-Code, X-TPP-Code, PSU-Initiated.
- Sistem tetikli sorgu günde 4, 24 saat penceresi → backend veriyi kendi DB'sinde tutar, olaylarla tazeler.
- Tutarlar string ("1234.50"). islAmc kodları: 01 konut kirası, 05 maaş, 11 aidat… (bkz. docs).
