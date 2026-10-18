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

## Doğrulanması gerekenler

Spesifikasyondan henüz doğrulanamayan ve varsayımla ilerlenen noktalar:

- **HHS-YÖS API (banka listesi):** Gerçek ortamda `banks` tablosu bu API'den dolacak. Uç adı ve alan adları incelenmedi; şimdilik liste `appsettings` → `Hhs:Banks`'ten geliyor (`api/src/FinCopilot.Infrastructure/Hhs/BankCatalog.cs`).
- **Kart detay (08) ve kart hareketleri (09) alan adları:** bkz. [ÖHVPS referansı](docs/ohvps-hesap-bilgisi-referans.md) §8. Backend Mock HHS'nin kullandığı adlara göre yazıldı (`hesapKesimTarihi`, `donemBorcu`, `limitTutari`, `islemTutari`, `taksitSayisi`, `dovizBilgisi`…; `api/src/FinCopilot.Infrastructure/Hhs/OhvpsModels.cs`). Nakit limit alanları mock'ta yok.
- **İşlem listesi dizi adı:** Mock HHS `isller` kullanıyor (`{"hspRef", "isller": [...]}`); spesifikasyondaki adla karşılaştırılmalı.
- **Sayfalama başlıkları:** `X-Total-Count` / `X-Total-Pages` mock varsayımı; sistem tetikli sorguda her sayfanın kotadan düşüp düşmediği spesifikasyondan doğrulanmalı (mock'ta düşüyor).
- **Hata yanıtı gövdesi:** Mock HHS `errorCode` / `moreInformationTr` kullanıyor; spesifikasyondaki hata nesnesiyle karşılaştırılmalı (`mock-hhs/app/errors.py`).
- **Sistem tetikli kota kapsamı:** Mock HHS günde 4 sorguyu *yol başına* sayıyor (`/bakiye`, `/hesaplar/{ref}/bakiye`, `/hesaplar/{ref}/islemler` ayrı). Backend buna göre tasarlandı (BAKIYE olayında `/hesaplar/{ref}/bakiye` kullanılır); spesifikasyonda kotanın rıza başına mı yol başına mı olduğu doğrulanmalı.
- **Ayrık GKD'de yetKod'un iletimi:** Mock HHS, onaydan sonra yetKod'u rıza sorgusunda (`gkd.yetKod`) döndürüyor. Spesifikasyondaki yöntem doğrulanmalı.
- **Olay gövdesi ve imza:** `POST /olay-dinleme` gövdesi Mock HHS'nin gönderdiği alanlara göre (`olayNo, olayAbonelikNo, olayTipi, kaynakTipi, kaynakNo, olayZamani, katilimciBlg`). `POST /sistem-olay-dinleme` gövdesi bilinmiyor (sadece saklanıyor). Gerçek ortamda `X-JWS-Signature` doğrulaması eklenmeli.
- **v2.0.1 farkları:** Sitede v2.0.1 yayınlanmış; v2.0.0 ile farkları kontrol edilmeli.

## Not

Bu proje bir bitirme projesidir. Gerçek banka bağlantısı yoktur; veri `mock-hhs/` üzerindeki sahte banka sunucusundan ve sentetik veriden gelir. Uygulama yalnızca okuma yapar, para hareketi başlatmaz ve yatırım tavsiyesi vermez.
