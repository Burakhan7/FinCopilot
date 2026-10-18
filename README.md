# FinCopilot

**Yapay zekâ destekli, bankadan bağımsız kişisel finans koçu.**

Kullanıcının farklı bankalardaki hesap ve kart verilerini Açık Bankacılık (TCMB ÖHVPS v2.0.0, Hesap Bilgisi Hizmeti) üzerinden toplar, anlamlandırır ve proaktif öneriler sunar:

- Tüm bankalarda tek mali tablo: nakit, borç, ileri tarihli yükümlülükler
- Otomatik harcama kategorizasyonu ve abonelik tespiti
- Kart kesim/son ödeme takibi, en kârlı kart ve ödeme planı önerisi
- Nakit akışı tahmini, günlük harcanabilir tutar, yeni masraf simülasyonu
- Doğal dilde soru sorma (LLM + function calling; hesaplamaları backend yapar)

> İlke: **LLM konuşur, backend hesaplar, kural motoru karar verir.**

## Repo yapısı

| Klasör | İçerik | Teknoloji |
|---|---|---|
| `mobile/` | Mobil istemci | Flutter |
| `api/` | Ana backend: kullanıcı, rıza, senkronizasyon, kural motoru | .NET 10, Clean Architecture, CQRS/MediatR, EF Core |
| `ai-service/` | Kategorizasyon, tahmin, LLM orkestrasyonu | Python, FastAPI |
| `mock-hhs/` | ÖHVPS şemalarına uyumlu sahte banka (HHS) sunucusu ve GKD simülasyonu | Python, FastAPI |
| `docs/` | Spesifikasyon notları, fonksiyon listesi, yol planı | Markdown |

## Dokümanlar

- [Sistem mimarisi](docs/mimari.md)
- [Veritabanı şeması](docs/veritabani.md)
- [ÖHVPS Hesap Bilgisi referansı](docs/ohvps-hesap-bilgisi-referans.md)
- [Backend fonksiyon listesi](docs/backend-fonksiyon-listesi.md)
- [Fonksiyon / veri kaynağı / sorun tablosu (PDF)](docs/fincopilot-fonksiyon-tablosu.pdf)
- [Yol planı](docs/yol-plani.md)

## Durum

Faz 0 – Hazırlık (Ekim 2026). Ayrıntılar için [yol planı](docs/yol-plani.md).

## Not

Bu proje bir bitirme projesidir. Gerçek banka bağlantısı yoktur; veri `mock-hhs/` üzerindeki sahte banka sunucusundan ve sentetik veriden gelir. Uygulama yalnızca okuma yapar, para hareketi başlatmaz ve yatırım tavsiyesi vermez.
