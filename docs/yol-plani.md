# FinCopilot – Yol Planı (v1)

Varsayımlar: Ekim 2026 – Haziran 2027, bitirme teslimi Haziran 2027. Güz dönemi sonunda (Ocak) ara rapor ve demo var. Okul takvimine göre tarihler kaydırılır.

## Teknoloji önerisi
| Katman | Teknoloji |
|---|---|
| Mobil | Flutter + Riverpod |
| Ana API | .NET 10 LTS, Clean Architecture, CQRS/MediatR 12, EF Core 10 |
| Veri / AI servisi | Python FastAPI (kategorizasyon, tahmin, LLM orkestrasyonu) |
| Mock HHS | FastAPI (ÖHVPS v2.0.0 şemalarına birebir) |
| Veritabanı | Microsoft SQL Server 2022 |
| Arka plan işleri | Hangfire (.NET) |
| Bildirim | Firebase FCM |
| LLM | Gemini (function calling) + Ollama yerel model (karşılaştırma için) |
| Dağıtım | Docker Compose, Hetzner VPS, GitHub Actions |

## Alınan kararlar
| Konu | Karar | Tarih |
|---|---|---|
| Veritabanı | Microsoft SQL Server 2022 | 14.10.2026 |
| Mock HHS | Python FastAPI | 14.10.2026 |
| Arka plan işleri | Hangfire (SQL Server deposu) | 14.10.2026 |
| Ana LLM | Gemini (karşılaştırma için yerel model Ollama) | 14.10.2026 |
| Flutter state management | Riverpod | 14.10.2026 |
| .NET sürümü | .NET 10 LTS (.NET 8 desteği 10.11.2026'da bitiyor) | 18.10.2026 |
| Mediator | MediatR 12.5.0 (son Apache-2.0 sürüm; 13+ ticari) | 18.10.2026 |
| Kullanıcı girişi | Kendi sistemimiz: telefon + SMS kodu + PIN + JWT | 18.10.2026 |
| Hassas veri şifreleme | ASP.NET Data Protection, anahtarlar SQL Server'da | 18.10.2026 |
| Mapping | Elle (kütüphane yok) | 18.10.2026 |
| Doğrulama | FluentValidation (MediatR pipeline) | 18.10.2026 |
| Test | xUnit + Testcontainers (SQL Server 2022) | 18.10.2026 |
| Geliştirme DB | Docker'da SQL Server 2022 (`docker-compose.yml`) | 18.10.2026 |
| PIN hash | ASP.NET PasswordHasher (PBKDF2) | 18.10.2026 |
| JWT | HS256, access 15 dk, refresh 30 gün (DB'de, rotation + tekrar kullanım tespiti) | 18.10.2026 |
| TCKN alma anı | İlk banka bağlamada (kayıtta değil) | 18.10.2026 |
| Yeni cihaz girişi | SMS + PIN; kayıtlı cihazda sadece PIN | 18.10.2026 |
| TCKN arama özeti | HMAC-SHA256, anahtar config'te (`Security__TcknHashKey`) | 18.10.2026 |
| HHS HTTP dayanıklılığı | Microsoft.Extensions.Http.Resilience; retry sadece GET | 18.10.2026 |
| Banka listesi | `Hhs:Banks` yapılandırması → açılışta `banks` tablosuna | 18.10.2026 |
| Ayrık GKD | Olay dinleme ile birlikte (Faz 2, 5. adım) | 18.10.2026 |

Not: Teknoloji ve mimari kararları Burakhan verir; yeni bir karar gerektiğinde seçenekler sunulur.

## Fazlar
| Faz | Tarih | Odak | Çıktı / Kilometre taşı |
|---|---|---|---|
| 0. Hazırlık | 14–31 Eki | Kapsamı dondurma, öneri formu, mimari, DB şeması, repo, CI | Hoca onayı, mimari diyagram, ER diyagramı, boş ama çalışan repo |
| 1. Mock HHS + sentetik veri | Kasım | Rıza, sahte banka GKD sayfası, token, hesap/bakiye/işlem/kart/kart-detay/kart-hareketleri, olay bildirimi; 3 banka × 3 persona × 12 ay veri üretici | Swagger'dan uçtan uca rıza → token → veri çekme |
| 2. Çekirdek backend + mobil iskelet | Aralık | Kullanıcı kaydı (SMS OTP, PIN/biyometrik), rıza modülü (durum makinesi), senkronizasyon (backfill, zamanlı, olay), Flutter: onboarding + banka bağla akışı | Telefonda "Banka bağla → bankada onay → hesaplar geldi" |
| 3. Zenginleştirme + ara demo | Ocak | Mağaza normalizasyonu, kategorizasyon (islAmc → Regex → ML), tekrar eden kalem tespiti + onay ekranı | **Güz ara raporu ve demo**, kategorizasyon doğruluk metriği |
| 4. Analitik + kural motoru | Şubat | Mali tablo, harcama dağılımı, kart döngüleri, kart önerisi, ödeme planı, nakit akışı tahmini, günlük limit, masraf simülasyonu; Flutter dashboard | Ana ekran tam çalışır, kural motoru birim testleri |
| 5. LLM katmanı | Mart | Tool tanımları, function calling, PII maskeleme, rakam doğrulama, 50–100 soruluk değerlendirme seti, Gemini vs yerel model | Sohbet ekranı, doğruluk/halüsinasyon/gecikme tablosu |
| 6. Bildirim + para kazandıranlar | Nisan | Uyarı kuralları, sabah brifingi, maaş günü planı, KMH kurtarıcı, masraf avcısı, kazanç sayacı, widget | Uygulama "proaktif" hale gelir |
| 7. Test + cilalama | Mayıs | Entegrasyon ve yük testleri, güvenlik kontrolleri, UI cilası, zaman kalırsa F2 özellikleri | Özellik dondurma (feature freeze) |
| 8. Teslim | Haziran başı | Final rapor, sunum, demo videosu | Jüri |

## Riskler
| Risk | Önlem |
|---|---|
| Kapsam şişmesi | Ocak'tan sonra yeni özellik yok, F2 listesi sadece Mayıs'ta |
| Kategorizasyon için etiketli veri yok | Sentetik veri + elle etiketlenmiş 500–1.000 gerçekçi işlem açıklaması |
| LLM maliyeti / kota | Önbellek, kısa bağlam, yerel model yedeği |
| Kart detay / hareket alan adları belirsiz | Faz 1'de spesifikasyondan elle doğrulama |
