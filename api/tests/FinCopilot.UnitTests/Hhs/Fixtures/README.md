# Mock HHS sözleşme fixture'ları

Gerçek `mock-hhs` sunucusundan alınmış yanıtlar (persona: Zeynep, 10000000382, Ege Bank 9002; tüm izinler).
`MockHhsDataTests` bunları `MockHhsClient` ile çözer. Mock HHS'nin yanıt şeması değişirse yeniden alınmalı:
rıza aç → sahte GKD'de onayla → token → her veri ucunu `PSU-Initiated: E` ile çağır.

| Dosya | Uç |
|---|---|
| `hesaplar.json` | `GET /hesaplar` |
| `bakiye.json` | `GET /bakiye` |
| `islemler.json` | `GET /hesaplar/{hspRef}/islemler` (30 gün; `X-Total-Count: 9`) |
| `kartlar.json` | `GET /kartlar` |
| `kart-detay.json` | `GET /kartlar/{kartRef}/kart-detay` |
| `kart-hareketleri.json` | `GET /kartlar/{kartRef}/kart-hareketleri` (taksit ve döviz örnekli pencere; `X-Total-Count: 31`) |
| `hata-pencere.json` | 40 günlük pencere → `400 TR.OHVPS.Business.InvalidStartEndTime` |
