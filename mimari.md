# Sistem Mimarisi

> İlke: **LLM konuşur, backend hesaplar, kural motoru karar verir.**

## 1. Genel görünüm

```mermaid
flowchart LR
    subgraph Client["Mobil (Flutter)"]
        APP["FinCopilot App"]
    end

    subgraph Backend["Bizim backend"]
        API["Core API<br/>.NET 8"]
        WORKER["Worker<br/>Hangfire işleri"]
        AI["AI Service<br/>FastAPI"]
        DB[("SQL Server")]
    end

    subgraph Bank["Banka tarafı (simülasyon)"]
        HHS["Mock HHS<br/>FastAPI"]
        GKD["Sahte banka<br/>GKD sayfası"]
    end

    subgraph External["Dış servisler"]
        LLM["Gemini API"]
        OLLAMA["Ollama<br/>yerel model"]
        FCM["Firebase FCM"]
        TUIK["TCMB / TÜİK<br/>faiz, TÜFE"]
    end

    APP -- "HTTPS + JWT" --> API
    APP -. "GKD yönlendirme" .-> GKD
    GKD -. "yetKod + drmKod<br/>deep link" .-> APP

    API --> DB
    WORKER --> DB
    API -- "ÖHVPS çağrıları<br/>(IHhsClient)" --> HHS
    WORKER -- "zamanlı senkron<br/>≤4/gün" --> HHS
    HHS -- "POST /olay-dinleme" --> API

    API -- "kategorize, sohbet" --> AI
    AI -- "tool çağrısı<br/>/internal/tools/*" --> API
    AI --> LLM
    AI -.-> OLLAMA

    WORKER --> FCM
    FCM -.-> APP
    WORKER --> TUIK
```

## 2. Bileşenler ve sorumluluklar

| Bileşen | Sorumluluk | Teknoloji |
|---|---|---|
| **Mobile** | Arayüz, banka bağlama akışı, sohbet ekranı, widget, push alma | Flutter |
| **Core API** | Kimlik, rıza yönetimi, ÖHVPS istemcisi, analitik ve kural motoru, LLM tool uçları, olay dinleme | .NET 8, Clean Architecture, CQRS/MediatR, EF Core |
| **Worker** | Zamanlı senkronizasyon, uyarı kuralları, sabah brifingi, rıza süresi kontrolü, faiz ve TÜFE güncelleme | Hangfire (Core API ile aynı çözüm, ayrı süreç) |
| **AI Service** | Kategorizasyon (Regex → ML → LLM fallback), tekrarlayan kalem tespiti, LLM orkestrasyonu, PII maskeleme, rakam doğrulama | Python, FastAPI, scikit-learn |
| **Mock HHS** | ÖHVPS v2.0.0 Hesap Bilgisi uçları, sahte GKD sayfası, sentetik veri, olay gönderimi | Python, FastAPI |
| **SQL Server** | Tüm kalıcı veri + Hangfire iş deposu (bkz. [veritabani.md](veritabani.md)) | SQL Server 2022 |

### Hesaplama nerede yapılır?
- **Deterministik her şey Core API'de:** bakiye, borç, kart döngüsü, kart önerisi, nakit akışı, günlük limit, simülasyon. Birim testleri burada.
- **Olasılıksal her şey AI Service'te:** kategori tahmini, tekrar örüntüsü tespiti, doğal dil.
- AI Service veritabanına **doğrudan erişmez**. Veriyi sadece Core API'nin `/internal/tools/*` uçlarından okur.

## 3. Core API katmanları (Clean Architecture)

```mermaid
flowchart TB
    PRES["Api<br/>Controllers, olay dinleme, internal tool uçları"]
    APPL["Application<br/>Commands / Queries (MediatR), DTO, validasyon"]
    DOM["Domain<br/>Entity'ler, rıza durum makinesi, kural motoru, tahmin motoru"]
    INFRA["Infrastructure<br/>EF Core, IHhsClient (Mock / BKM / Gerçek), FCM, şifreleme"]

    PRES --> APPL
    APPL --> DOM
    INFRA --> APPL
    INFRA --> DOM
```

`IHhsClient` bir **adapter** arayüzü: `MockHhsClient` (bitirme), `BkmSimulatorClient` (opsiyonel), `LicensedProviderClient` (gelecek). Gerçek ortama geçiş = yeni adapter + konfigürasyon.

## 4. Akışlar

### 4.1 Banka bağlama (rıza + yönlendirmeli GKD)

```mermaid
sequenceDiagram
    autonumber
    actor U as Kullanıcı
    participant M as Mobile
    participant A as Core API
    participant H as Mock HHS
    participant G as Sahte GKD sayfası

    U->>M: "Banka bağla" + izinleri onayla
    M->>A: POST /consents {hhsKod}
    A->>A: drmKod üret, consent kaydı (B)
    A->>H: POST /hesap-bilgisi-rizasi
    H-->>A: rizaNo, rizaDrm=B, hhsYonAdr
    A-->>M: hhsYonAdr
    M->>G: Yönlendir (tarayıcı / universal link)
    U->>G: TCKN + şifre + SMS, hesap/kart seç, onayla
    G-->>M: deep link: yetKod, drmKod, rizaNo
    M->>A: POST /consents/{id}/callback
    A->>A: drmKod doğrula
    A->>H: POST /erisim-belirteci (yet_kod)
    H-->>A: erisimBelirteci, yenilemeBelirteci
    A->>A: Tokenları şifreli sakla, consent = K
    A->>H: POST /olay-abonelik
    A-)A: initial_backfill işini kuyruğa at
    A-->>M: Bağlandı
```

### 4.2 Senkronizasyon

```mermaid
sequenceDiagram
    autonumber
    participant W as Worker
    participant A as Core API
    participant H as Mock HHS
    participant AI as AI Service
    participant DB as SQL Server

    Note over W: İlk yükleme: PSU-Initiated=E, 12 ay, 1 aylık pencereler
    Note over W: Zamanlı: PSU-Initiated=H, 24 saat penceresi, günde ≤4
    W->>DB: sync_jobs kotası uygun mu?
    W->>H: GET /hesaplar, /bakiye, /islemler, /kartlar, /kart-detay, /kart-hareketleri
    H-->>W: Ham ÖHVPS verisi
    W->>DB: Upsert (islNo / islemNo ile tekil)
    W->>AI: POST /categorize (yeni işlemler)
    AI-->>W: kategori, mağaza, kaynak (islAmc/regex/ml/llm)
    W->>DB: Zenginleştirilmiş veri
    W->>W: detect_recurring_items, evaluate_alert_rules
    H--)A: POST /olay-dinleme (KAYNAK_GUNCELLENDI, BAKIYE)
    A->>DB: event_inbox (idempotent) → hedefli senkron
```

### 4.3 Sohbet (LLM + tool çağrısı)

```mermaid
sequenceDiagram
    autonumber
    actor U as Kullanıcı
    participant M as Mobile
    participant A as Core API
    participant AI as AI Service
    participant L as Gemini

    U->>M: "Bu ay dışarıda yemeğe ne harcadım?"
    M->>A: POST /chat
    A->>AI: POST /chat {userContextToken, mesaj}
    AI->>L: mesaj + tool tanımları
    L-->>AI: tool_call: get_spending(category="yeme-icme", period="2026-10")
    AI->>A: GET /internal/tools/get_spending
    A-->>AI: {toplam: 2840.00, adet: 14}
    AI->>L: tool sonucu (PII maskeli)
    L-->>AI: "Bu ay 2.840 TL, 14 sipariş"
    AI->>AI: Rakam doğrulama (cevap ↔ tool çıktısı)
    AI-->>A: cevap
    A-->>M: cevap
```

## 5. Güvenlik kararları

| Konu | Karar |
|---|---|
| Banka şifresi | Hiç görülmez; sadece sahte banka sayfasına girilir |
| ÖHVPS tokenları | Veritabanında AES-GCM ile şifreli, mobile asla inmez |
| TCKN, IBAN | Şifreli saklanır; arama için ayrı hash sütunu |
| LLM'e giden veri | PII maskeli, sadece tool çıktısı ve özet |
| LLM yetkisi | Sadece okuma tool'ları; yazma yok |
| Internal tool uçları | Sadece servis ağı + kısa ömürlü, kullanıcıya bağlı servis token'ı |
| GKD dönüşü | `drmKod` ile CSRF koruması, tek kullanımlık |
| Olay dinleme | `olayNo` ile idempotent; mock'ta imza opsiyonel, gerçekte JWS zorunlu |

## 6. Ortamlar

| Ortam | İçerik |
|---|---|
| Yerel | `docker compose up`: SQL Server, Core API, Worker, AI Service, Mock HHS |
| Demo | Aynı compose, Hetzner VPS, GitHub Actions ile dağıtım |
