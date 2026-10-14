# FinCopilot – Yol Planı (v1)

Varsayımlar: Ekim 2026 – Haziran 2027, bitirme teslimi Haziran 2027. Güz dönemi sonunda (Ocak) ara rapor ve demo var. Okul takvimine göre tarihler kaydırılır.

## Teknoloji önerisi
| Katman | Teknoloji |
|---|---|
| Mobil | Flutter |
| Ana API | .NET 8, Clean Architecture, CQRS/MediatR, EF Core |
| Veri / AI servisi | Python FastAPI (kategorizasyon, tahmin, LLM orkestrasyonu) |
| Mock HHS | FastAPI (ÖHVPS v2.0.0 şemalarına birebir) |
| Veritabanı | PostgreSQL ya da MSSQL |
| Arka plan işleri | Hangfire (.NET) |
| Bildirim | Firebase FCM |
| LLM | Gemini (function calling) + Ollama yerel model (karşılaştırma için) |
| Dağıtım | Docker Compose, Hetzner VPS, GitHub Actions |

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
