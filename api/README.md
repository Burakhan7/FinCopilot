# api

.NET 8 ana backend (Clean Architecture, CQRS/MediatR, EF Core).

Modüller:
- Kimlik ve oturum
- Rıza yönetimi (B → Y → K → S/I durum makinesi)
- Senkronizasyon (ilk yükleme, zamanlı sorgu ≤4/gün, olay dinleme `POST /olay-dinleme`)
- Analitik ve kural motoru
- Bildirimler (FCM)

Fonksiyonlar: [docs/backend-fonksiyon-listesi.md](../docs/backend-fonksiyon-listesi.md)
