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

Üretimde zorunlu: `Jwt__SigningKey` (≥32 bayt) ortam değişkeni. `Auth:OtpFixedCode` Development/Testing dışında verilirse Api açılmaz.

## Test
```
dotnet test api/FinCopilot.sln
```

## Yeni migration
```
dotnet ef migrations add <Ad> --project api/src/FinCopilot.Infrastructure --startup-project api/src/FinCopilot.Api --output-dir Persistence/Migrations
```

Fonksiyonlar: [docs/backend-fonksiyon-listesi.md](../docs/backend-fonksiyon-listesi.md) · Şema: [docs/veritabani.md](../docs/veritabani.md)
