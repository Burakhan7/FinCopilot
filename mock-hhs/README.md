# mock-hhs

TCMB ÖHVPS v2.0.0 Hesap Bilgisi Hizmeti şemalarına uyumlu sahte banka (HHS) sunucusu.

Kapsam (Faz 1):
- `POST/GET/DELETE /hesap-bilgisi-rizasi`
- Sahte banka GKD sayfası (yönlendirmeli) + ayrık GKD simülasyonu
- `POST /erisim-belirteci`
- `/hesaplar`, `/bakiye`, `/hesaplar/{hspRef}/islemler`, `/kartlar`, `/kartlar/{kartRef}/kart-detay`, `/kartlar/{kartRef}/kart-hareketleri`
- Olay bildirimi (`KAYNAK_GUNCELLENDI`, `AYRIK_GKD_BASARILI/BASARISIZ`)
- Sentetik veri üretici: 3 banka × 3 persona × 12 ay

Referans: [docs/ohvps-hesap-bilgisi-referans.md](../docs/ohvps-hesap-bilgisi-referans.md)
