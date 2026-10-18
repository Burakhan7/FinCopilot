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
  FinCopilot.IntegrationTests Testcontainers ile gerçek SQL Server 2022 (Docker Desktop açık olmalı)
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

## Yapılandırma
Üretimde zorunlu ortam değişkenleri: `Jwt__SigningKey`, `Security__TcknHashKey` (ikisi de ≥32 bayt). `Auth:OtpFixedCode` Development/Testing dışında verilirse Api açılmaz. Banka listesi ve YÖS kodu: `Hhs` bölümü (bkz. `appsettings.Development.json`).

## Test
```
dotnet test api/FinCopilot.sln
```

## Yeni migration
```
dotnet ef migrations add <Ad> --project api/src/FinCopilot.Infrastructure --startup-project api/src/FinCopilot.Api --output-dir Persistence/Migrations
```

Fonksiyonlar: [docs/backend-fonksiyon-listesi.md](../docs/backend-fonksiyon-listesi.md) · Şema: [docs/veritabani.md](../docs/veritabani.md)
