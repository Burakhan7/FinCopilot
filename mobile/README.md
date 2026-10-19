# mobile

Flutter mobil istemci.

## Kurulum

```bash
flutter pub get
dart run build_runner build --delete-conflicting-outputs
flutter run
```

## Teknolojiler

| Paket | Amaç |
|---|---|
| flutter_riverpod | State management |
| go_router | Navigasyon + deep link (GKD dönüşü) |
| dio | HTTP istemcisi |
| flutter_secure_storage | JWT, PIN güvenli depolama |
| freezed + json_serializable | Model sınıfları (code gen) |
| pin_code_fields | OTP ve PIN giriş alanları |
| local_auth | Biyometrik kilit açma |
| firebase_messaging | Push bildirimler |
| app_links | GKD deep link yakalama |

## Klasör yapısı

```
lib/
├── main.dart                       # Uygulama giriş noktası
├── core/
│   ├── api/                        # Dio client, auth interceptor, hata yönetimi
│   ├── constants/                  # API endpoint ve uygulama sabitleri
│   ├── storage/                    # Güvenli depolama servisi
│   ├── theme/                      # Renk paleti, Material 3 tema
│   ├── utils/                      # Result tipi, yardımcılar
│   └── widgets/                    # Paylaşılan widget'lar
├── features/
│   ├── splash/                     # Açılış ekranı, yönlendirme mantığı
│   ├── onboarding/                 # Tanıtım sayfaları (3 sayfa)
│   ├── auth/                       # Kayıt, OTP doğrulama, PIN
│   ├── consent/                    # Banka bağlama (rıza → GKD → callback)
│   ├── accounts/                   # Hesap ve kart listesi
│   ├── dashboard/                  # Ana sayfa, günlük limit, harcama özeti
│   └── chat/                       # AI sohbet ekranı
└── router/                         # GoRouter yapılandırması, bottom nav shell
```

## Ekranlar (Faz 2)

1. **Splash** — Token/PIN kontrolü → yönlendirme
2. **Onboarding** — 3 sayfalık tanıtım
3. **Login** — Telefon numarası girişi
4. **OTP** — SMS doğrulama (6 hane, geri sayım)
5. **PIN** — Oluşturma (2x giriş) ve kilit açma
6. **Home** — Günlük limit, bakiye, borç, yaklaşan ödemeler
7. **Accounts** — Bağlı hesap/kart listesi
8. **Bank Connect** — Banka seç → izinleri onayla → GKD'ye yönlendir
9. **Chat** — AI finans koçu sohbet ekranı

## Deep Link (GKD dönüşü)

```
fincopilot://callback?yetKod=...&drmKod=...&rizaNo=...
```

GKD onayından sonra banka bu URL'ye yönlendirir. `go_router` redirect'i yakalar ve consent callback'i tetikler.

## API Bağlantısı

- Base URL: `http://10.0.2.2:5000/api` (emülatör) — `.env` veya `--dart-define` ile override
- JWT auth: access + refresh token, otomatik yenileme (AuthInterceptor)
- Hata mesajları Türkçe (ApiException)
