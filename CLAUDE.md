# FinCopilot – Claude Code bağlamı

Bitirme projesi (Manisa Celal Bayar Üniv., teslim Haziran 2027). Geliştirici: Burakhan (GitHub: Burakhan7).
**Yapay zekâ destekli, bankadan bağımsız kişisel finans koçu.** TCMB ÖHVPS v2.0.0 Hesap Bilgisi Hizmeti
üzerinden birden çok bankadaki hesap/kart verisini toplar, anlamlandırır, proaktif öneri sunar.

> İlke: **LLM konuşur, backend hesaplar, kural motoru karar verir.** LLM sadece okuma yapan tool'ları çağırır.

## Çalışma kuralları
- **Teknoloji/mimari kararlarını kendin verme.** Seçenekleri (kısa artı/eksi + önerin) sun, Burakhan karar versin.
- Yanıtlar Türkçe, kısa ve doğrudan. Komutlar kopyala-yapıştır hazır (Windows cmd/PowerShell öncelikli).
- Her adım sonunda testleri çalıştır; geçmeden "bitti" deme.
- Spesifikasyondan doğrulanamayan şeyleri uydurma; README'deki "Doğrulanması gerekenler" listesine ekle.
- Dev DB'de elle SQL: `docker exec fincopilot-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "FinCopilot_Dev_2026!" -C -I -d FinCopilot -Q "..."` (`-I` şart: filtered index'ler QUOTED_IDENTIFIER ister).
- Commit mesajları Türkçe, kısa.

## Alınan kararlar
| Konu | Karar |
|---|---|
| Mobil | Flutter + Riverpod |
| Ana API | .NET 10 LTS, Clean Architecture, CQRS/MediatR **12.5.0** (13+ ticari, yükseltme), EF Core 10 |
| Veritabanı | Microsoft SQL Server 2022 |
| Arka plan işleri | Hangfire (SQL Server deposu) |
| Mock HHS | Python FastAPI (`mock-hhs/`, durumu SQLite'ta) |
| AI servisi | Python FastAPI (`ai-service/`) |
| Ana LLM | Gemini (function calling); karşılaştırma için Ollama yerel model |
| Banka kartı harcamaları | Sadece hesap hareketlerinden okunur (kart hareketlerinde tekrar yok) |
| Kullanıcı girişi | Kendi sistemimiz: telefon + SMS kodu + PIN + JWT. PIN: ASP.NET PasswordHasher. JWT HS256 (15 dk) + DB'de refresh (30 gün, rotation). Yeni cihaz: SMS + PIN, kayıtlı cihaz: PIN. TCKN ilk banka bağlamada alınır |
| Hassas veri şifreleme | ASP.NET Data Protection (`ISecretProtector`), anahtar halkası SQL Server'da (`data_protection_keys`) |
| Mapping | Elle (kütüphane yok) |
| Doğrulama | FluentValidation, MediatR `ValidationBehavior` ile |
| Test | xUnit + Testcontainers (gerçek SQL Server 2022; Docker Desktop açık olmalı) |
| Rıza modülü | TCKN ilk banka bağlamada (`PUT /me/tckn`), arama özeti HMAC-SHA256 (`Security__TcknHashKey`). Banka listesi `Hhs:Banks` config → açılışta `banks`'e upsert. HHS HTTP: Microsoft.Extensions.Http.Resilience (retry sadece GET). İzinler 01–09 hepsi, süre 180 gün, geçmiş 365 gün. Ayrık GKD 5. adımda |
| Geliştirme DB | Docker'da SQL Server 2022 (`docker-compose.yml`, sa şifresi `FinCopilot_Dev_2026!`) |

## Repo
```
docs/        mimari.md, veritabani.md (ER + tablolar), backend-fonksiyon-listesi.md,
             ohvps-hesap-bilgisi-referans.md, mock-veri-parametreleri.md, yol-plani.md
mock-hhs/    Sahte banka (bitti, 36 test)
api/         .NET backend (Faz 2 – iskelet + ilk migration hazır, bkz. api/README.md)
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
1. ✅ Solution iskeleti (Domain/Application/Infrastructure/Api/Worker), SQL Server, ilk migration — 13 birim + 7 integration testi
2. ✅ Kullanıcı kaydı/giriş (`/auth/*`, `/me`) — toplam 36 birim + 22 integration testi
3. ✅ Rıza modülü (`/banks`, `/me/tckn`, `/consents/*`): drmKod, durum makinesi (Domain), token şifreleme, HHS'den durum tazeleme — toplam 66 birim + 40 integration; gerçek Mock HHS ile elle uçtan uca denendi
4. `IHhsClient` adapter (MockHhsClient): rıza/token uçları hazır; veri uçları (hesaplar, bakiye, işlemler, kartlar…) eklenecek
5. Senkronizasyon: ilk 12 ay yükleme, Hangfire ile günde ≤4 sorgu, `POST /olay-dinleme`. Ayrıca: rıza K olunca olay aboneliği + initial_backfill tetikleme, ayrık GKD, süre dolumu işi (check_consent_expiry)
6. Uçtan uca test (Mock HHS + backend)

**Backend'i çalıştırma:** bkz. [api/README.md](api/README.md) (`docker compose up -d sqlserver` → `dotnet ef database update` → Api `http://localhost:8080`).

**Açık konular (sonra sorulacak):**
- Data Protection anahtarları DB'de düz XML; üretimde anahtarların sertifika/ortam anahtarıyla ayrıca şifrelenmesi (`ProtectKeysWith...`).
- Gerçek SMS sağlayıcısı (şimdilik `LoggingSmsSender` sadece loga yazar).
- Access token iptali: logout sadece refresh'i kapatır, access token en fazla 15 dk daha geçerli kalır.

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
