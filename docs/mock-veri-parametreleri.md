# Mock Veri Parametreleri

Mock HHS'nin ürettiği sentetik veri iki kaynağa dayanır:

1. **Veri formatı → TCMB ÖHVPS v2.0.0** (alan adları, kod listeleri, rıza ve sorgu kuralları). Ayrıntı: [ohvps-hesap-bilgisi-referans.md](ohvps-hesap-bilgisi-referans.md)
2. **Finansal kurallar ve tutarlar → BDDK, TCMB, TÜİK, BKM** resmi düzenleme ve istatistikleri (aşağıda).

Tüm değerler `mock-hhs/app/generator/params.py` içinde tek yerde durur; mevzuat değişirse sadece orası güncellenir.
Araştırma tarihi: 14.10.2026.

## 1. Düzenleyici parametreler

| Parametre | Değer | Kaynak |
|---|---|---|
| Kredi kartı azami akdi faiz (TL, aylık) | Dönem borcu < 30.000: **%3,25** · 30.000–180.000: **%3,75** · > 180.000: **%4,25** | TCMB, 01.10.2026'dan itibaren |
| Kredi kartı azami gecikme faizi (TL, aylık) | **%3,55** · **%4,05** · **%4,55** (aynı kademeler) | TCMB, 01.10.2026 |
| Nakit avans ve KMH azami faiz (aylık) | Akdi **%4,25** · gecikme **%4,55** | TCMB, 01.10.2026 |
| Yabancı para kart faizi (aylık) | Akdi %2,98 · gecikme %3,28 | TCMB, 01.10.2026 |
| Asgari ödeme oranı | Kart limiti ≤ 100.000 TL: **%20** · > 100.000 TL: **%40** | BDDK, Ekim 2026 kararı (eşik 50.000'den 100.000'e çıktı) |
| Toplam kart limiti tavanı | İlk yıl aylık net gelirin **2 katı**, sonraki yıllar **4 katı** (tüm bankalar toplamı) | 5464 s. Kanun m.9, BDDK 11367 s. Karar (29.01.2026) |
| Faize eklenen vergiler | BSMV **%15** + KKDF **%15** (faiz üzerinden) | Vergi mevzuatı |
| Cep telefonu taksiti | Sıfır telefona kartla taksit **yok**; yenilenmiş telefona 12 taksite kadar | BDDK, Ekim 2026 |
| FAST tek işlem limiti | 100.000 TL | TCMB FAST |

KMH faizine BSMV/KKDF uygulanması mock'ta **varsayım** olarak kabul edildi (doğrulanacak).

## 2. Gelir ve harcama kalibrasyonu

| Parametre | Değer | Kaynak |
|---|---|---|
| Asgari ücret (net, 2026) | 28.075,50 TL | 2026 asgari ücret |
| Hanehalkı harcama payları (2025) | Konut+kira **%29,3** · Ulaştırma **%20,5** · Gıda **%17,3** · Sağlık %2,2 · Eğitim %1,8 · Sigorta/finans %0,8 | TÜİK Hanehalkı Tüketim Harcaması 2025 |
| Tek kişilik hanede pay | Konut **%41** · Ulaştırma %14,4 · Gıda %14,3 | TÜİK 2025 |
| Ücretli hanede pay | Konut %26,4 · Ulaştırma %21,9 · Gıda %16 | TÜİK 2025 |
| Kartlı ödeme ortalama işlem tutarı | ≈ 1.500 TL (Ağustos 2026) | BKM (2.990,4 milyar TL / 1.991,8 milyon adet) |
| İnternetten ödeme payı | Tutarın **%30**'u, adedin **%13**'ü | BKM Ağustos 2026 |
| Kredi kartı / banka kartı tutar payı | ~%85 / ~%15 | BKM Ağustos 2026 |
| Market ve AVM payı | İşlem adedinin ~yarısı, tutarın ~%20'si | TCMB 2026/1. çeyrek kartlı harcama verisi |

Personaların aylık bütçeleri bu paylara göre ayarlandı (öğrenci ve tek kişilik hanelerde konut payı yüksek, ücretlide ulaştırma payı yüksek).

## 3. Temsili (resmi kaynağı olmayan) değerler

Aşağıdakiler bankadan bankaya değişir; mock'ta gerçekçi aralıklarda **temsili** kullanılır:

- EFT/havale ücretleri (mobil): 5–10 TL; FAST bireysel mobilde çoğunlukla ücretsiz
- Hesap işletim ücreti, kart yıllık ücreti
- Abonelik fiyatları (Netflix, Spotify, Adobe vb.)
- İşyeri açıklama metinleri (POS/sanal POS formatları)

## 4. Kaynaklar

- [TCMB – Kredi Kartı İşlemlerinde Uygulanacak Azami Faiz Oranları](https://www.tcmb.gov.tr/wps/wcm/connect/TR/TCMB+TR/Main+Menu/Istatistikler/Bankacilik+Verileri/Kredi_Karti_Islemlerinde_Uygulanacak_Azami_Faiz_Oranlari)
- [BirGün – Kredi kartı cep telefonu taksit ve asgari ödeme (Ekim 2026)](https://www.birgun.net/haber/kredi-karti-cep-telefonu-taksit-sayisi-2026-cep-telefonu-kredisi-ve-kredi-kartlarinda-vade-siniri-ne-oldu-738728)
- [Kredi kartı limiti gelirin kaç katı – BDDK 11367](https://halilbakirci.av.tr/kredi-karti-limiti-gelirin-kac-kati-bddk-11367-2026/)
- [HesapKurdu – Kredi kartı BSMV](https://www.hesapkurdu.com/kredi-karti/rehber/kredi-karti-bsmv-nedir)
- [TÜİK 2025 hanehalkı harcaması (A Haber)](https://www.ahaber.com.tr/ekonomi/2026/06/02/tuik-verileri-acikladi-2025te-parayi-en-cok-neye-harcadik)
- [BKM Ağustos 2026 verileri (FinTech İstanbul)](https://fintechistanbul.org/2026/09/17/bkm-2026-yili-agustos-ayi-verilerini-acikladi/)
- [Kartlı harcamada sektör dağılımı – 2026/1. çeyrek (Yeniçağ)](https://www.yenicaggazetesi.com/kartli-harcamada-yeni-donem-gecimden-sonra-en-buyuk-pay-devlete-1018744h.htm)
- [2026 asgari ücret](https://asgariucret.co/asgari-ucret-2026)
