# api

.NET 10 ana backend (Clean Architecture, CQRS/MediatR 12, EF Core 10, SQL Server 2022, Hangfire).

## Yapı
```
src/
  FinCopilot.Domain          Entity'ler, ÖHVPS kodları (paket bağımlılığı yok)
  FinCopilot.Application     MediatR komut/sorguları, FluentValidation, IAppDbContext, ISecretProtector
  FinCopilot.Infrastructure  EF Core (AppDbContext, Configurations, Migrations), Data Protection
  FinCopilot.Api             HTTP uçları, /health, OpenAPI (/openapi/v1.json, sadece Development)
  FinCopilot.Worker          Hangfire sunucusu (zamanlı senkron vb.)
tests/
  FinCopilot.UnitTests        Docker gerektirmez
  FinCopilot.IntegrationTests Testcontainers ile gerçek SQL Server 2022, HHS sahte (FakeHhsClient)
  FinCopilot.E2ETests         Gerçek Mock HHS (mock-hhs/Dockerfile'dan container) + SQL Server + Api (Kestrel) + Hangfire
```
Paket sürümleri tek yerde: `Directory.Packages.props`. Uyarılar hata sayılır (açıklı paketler dahil).

## Çalıştırma (PowerShell, repo kökünden)
```
docker compose up -d sqlserver
dotnet ef database update --project api/src/FinCopilot.Infrastructure --startup-project api/src/FinCopilot.Api
dotnet run --project api/src/FinCopilot.Api        # http://localhost:8080/health
dotnet run --project api/src/FinCopilot.Worker
```

## Kimlik uçları
| Uç | Açıklama |
|---|---|
| `POST /auth/otp` `{phone}` | SMS kodu gönderir → `{challengeId, expiresAt}`. Development'ta kod hep `123456`, log'a `[SAHTE SMS]` olarak düşer |
| `POST /auth/register` | `{phone, challengeId, code, fullName, pin, deviceId, platform}` → token'lar |
| `POST /auth/login` | `{phone, pin, deviceId, platform, challengeId?, code?}`; yeni cihazda kodsuz istek `403 device_verification_required` |
| `POST /auth/refresh` `{refreshToken}` | Token'ları döndürür; eski refresh tekrar kullanılırsa oturum kapanır |
| `POST /auth/logout` | JWT ile; oturumu kapatır |
| `GET /me` | JWT ile; profil |

Hatalar ProblemDetails; istemci `code` alanına göre dallanır (`otp_invalid`, `otp_cooldown`, `otp_rate_limited`, `phone_already_registered`, `invalid_credentials`, `account_locked`, `device_verification_required`, `invalid_refresh_token`).

## Banka bağlama uçları (JWT)
| Uç | Açıklama |
|---|---|
| `GET /banks` | Bağlanabilir bankalar (`Hhs:Banks` yapılandırmasından) |
| `PUT /me/tckn` `{tckn}` | Bir kez; checksum kontrolü, şifreli saklama, HMAC ile tekillik |
| `POST /consents` `{hhsKod}` | HHS'de rıza açar → `201 {consentId, gkdUrl}`. Mobil `gkdUrl`'yi açar (tek kullanımlık `drmKod` içinde) |
| `POST /consents/{id}/callback` | `{rizaNo, drmKod, yetKod?}`: bankanın deep link ile döndürdüğü parametreler. yetKod → token → durum `K` |
| `GET /consents` | Bağlantılar (yerel veri) |
| `GET /consents/{id}` | Tek bağlantı; aktifse durum HHS'den tazelenir (bankadan iptal, süre aşımı) |
| `DELETE /consents/{id}` | HHS'de iptal (kod 03), token'lar silinir, hesap/kartlar pasifleşir |

Hata kodları: `tckn_already_set`, `tckn_in_use`, `tckn_required`, `bank_not_found`, `consent_already_active`, `consent_not_found`, `consent_not_pending`, `drm_kod_invalid`, `consent_not_active`, `bank_error` (502, `bankErrorCode` = ÖHVPS hata kodu).

Mobil olmadan denemek için: `$env:Hhs__RedirectUri="http://localhost:8090/_mock/callback"` ile Api'yi başlat; banka dönüşü Mock HHS'nin test sayfasına düşer, parametreler `GET /_mock/callback/{rizaNo}` ile okunur.

## Senkronizasyon
- Banka bağlanınca (`callback` → `K`) Api ilk yüklemeyi Hangfire'a atar; **Worker** çalışıyor olmalı. İlerleme: `GET /consents/{id}` → `lastSync {kind, status}` (`lastSync` boşsa iş henüz başlamadı).
- İlk yükleme: `PSU-Initiated=E`, rızanın işlem aralığı (≤12 ay), 30 günlük pencereler, tüm sayfalar.
- Zamanlı: Hangfire tekrarlayan işi `scheduled-sync`, her gün 00/06/12/18 (TR), rıza başına ayrı iş; `PSU-Initiated=H`, son 24 saat; rıza başına günde en fazla 4 (`sync_jobs`'tan sayılır).
- Hatalar: ağ/5xx → `failed` + Hangfire 3 deneme; 401 → token yenilenip 1 kez tekrar; 429 → `quota`; ConsentRevoked → rıza HHS'den tazelenir, `revoked`; diğer 4xx → `failed`, tekrar yok.
- Banka kartı hareketleri hesap hareketlerinden okunur; kart detay/hareketleri sadece kredi kartları için çekilir.
- Dashboard (Development): `http://localhost:8080/hangfire`

| Uç (JWT) | Açıklama |
|---|---|
| `GET /accounts` | Aktif hesaplar, maskeli IBAN ve son bakiye |
| `GET /cards` | Aktif kartlar; kredi kartında son ekstre |

## Olaylar ve ayrık GKD
| Uç (anonim, HHS çağırır) | Açıklama |
|---|---|
| `POST /olay-dinleme` | `yosKod`/`hhsKod` doğrulanır, `event_inbox`'a `olayNo` ile tekil yazılır, **202**; işleme Worker'da |
| `POST /sistem-olay-dinleme` | Kaydedilir, 202 (işlenmez) |

- `KAYNAK_GUNCELLENDI/BAKIYE` (kaynakNo = hspRef) → sadece o hesabın bakiyesi (`GET /hesaplar/{ref}/bakiye`, `source=event`), rıza başına günde ≤4; işlemler sıradaki zamanlı senkronda.
- `KAYNAK_GUNCELLENDI/HESAP_BILGISI_RIZASI`, `AYRIK_GKD_BASARISIZ` → rıza durumu HHS'den tazelenir.
- `AYRIK_GKD_BASARILI` → rıza sorgusundan yetKod → token → `K` → ilk yükleme.
- Ayrık GKD: `POST /consents {hhsKod, gkdYontem: "A"}` → `gkdUrl` yok; kullanıcı bankanın uygulamasında onaylar. Olay kaçarsa `GET /consents/{id}` aynı tamamlamayı yapar. Banka `SupportsDecoupled` değilse `400 decoupled_not_supported`.
- Olay abonelikleri: Worker açılışında ve her gün 02:00 (banka başına GET → POST/PUT).
- Rıza bakımı her gün 03:00: süresi geçen → `S`; 1 saattir B/Y'de kalan → HHS'den tazelenir; 7 gün içinde bitecek → `alerts` (`consent_expiring`, tekil).

Mock HHS'nin olayları Api'ye göndermesi için: `$env:MOCK_HHS_YOS_EVENT_URL="http://localhost:8080/olay-dinleme"` ile başlat.

## Yapılandırma
Worker da `Hhs` bölümüne (YÖS kodu + banka adresleri) ihtiyaç duyar; Api ile aynı olmalı (`src/FinCopilot.Worker/appsettings.Development.json`).

Üretimde zorunlu ortam değişkenleri: `Jwt__SigningKey`, `Security__TcknHashKey` (ikisi de ≥32 bayt). `Auth:OtpFixedCode` Development/Testing dışında verilirse Api açılmaz. Banka listesi ve YÖS kodu: `Hhs` bölümü (bkz. `appsettings.Development.json`).

## Test
```
dotnet test api/FinCopilot.sln
```
Docker Desktop açık olmalı (entegrasyon ve uçtan uca testler). İlk çalıştırmada Mock HHS imajı build edilir (~1 dk).

Uçtan uca senaryolar (`FinCopilot.E2ETests`): mobilin yaptığını yapar, banka sayfalarını HTML formlarıyla doldurur, deep link'i `Location` başlığından okur.
- Yönlendirmeli GKD → 12 aylık ilk yükleme (KMH, taksit, banka kartı kuralı) → zamanlı senkron → uygulamadan iptal (bankada 03)
- Ayrık GKD → olayla bağlanma (polling yok) → maaş yattı → BAKIYE olayı → bankadan iptal olayı (02)
- Bankada vazgeçme (13), bankaya başka kimlikle giriş (08)

CI: `.github/workflows/ci.yml`. Her push/PR'da Mock HHS pytest + .NET build ve üç test projesi sırayla.

## Yeni migration
```
dotnet ef migrations add <Ad> --project api/src/FinCopilot.Infrastructure --startup-project api/src/FinCopilot.Api --output-dir Persistence/Migrations
```

Fonksiyonlar: [docs/backend-fonksiyon-listesi.md](../docs/backend-fonksiyon-listesi.md) · Şema: [docs/veritabani.md](../docs/veritabani.md)
