# ÖHVPS v2.0.0 – Hesap Bilgisi Hizmeti (HBH) Referansı

Kaynak: https://ohvps.github.io/v2.0.0/ (Deney Alanı + contents sayfaları). 2026-09-29 tarihinde incelendi.
Not: Sitede v2.0.1 sürümü de yayınlanmış, farkları ayrıca kontrol edilmeli.

## 1. Deney Alanı'nın gerçekte ne olduğu
- Üyelik istemiyor ve herkese açık. Servisler stateless çalışıyor ve her istekte **aynı statik yanıtı** dönüyor.
- Oluşturma, güncelleme ve silme işlemleri veriyi değiştirmiyor.
- Dinamik veri ve sertifikasyon senaryoları için BKM ÖHVPS Simülatörü var (başvuru adresi: ohvps@bkm.com.tr).
- Sonuç: bitirme projesi için **kendi Mock HHS sunucumuzu** bu şemalara birebir uyacak şekilde yazmamız gerekiyor.

## 2. Endpointler (HHS tarafı, YÖS bunları çağırır)
| Kaynak | Metod | Yol | Gerekli izin |
|---|---|---|---|
| Rıza | POST | /hesap-bilgisi-rizasi | – |
| Rıza | GET | /hesap-bilgisi-rizasi/{rizaNo} | – |
| Rıza | DELETE | /hesap-bilgisi-rizasi/{rizaNo} | – (204 döner, durum I olur) |
| Token | POST | /erisim-belirteci | – |
| Hesaplar | GET | /hesaplar, /hesaplar/{hspRef} | 01 (+02 detay) |
| Bakiye | GET | /bakiye, /hesaplar/{hspRef}/bakiye | 03 |
| İşlemler | GET | /hesaplar/{hspRef}/islemler | 04 (+05 detay) |
| Kartlar | GET | /kartlar, /kartlar/{kartRef} | 07 |
| Kart detay | GET | /kartlar/{kartRef}/kart-detay | 08 |
| Kart hareketleri | GET | /kartlar/{kartRef}/kart-hareketleri | 09 |
| Olay aboneliği | POST/GET/PUT/DELETE | /olay-abonelik[/{olayAbonelikNo}] | – |
| İletilemeyen olaylar | GET | /olay-abonelik/{no}/iletilemeyen-olaylar | – |

YÖS'ün (bizim) açması zorunlu olanlar: `POST /olay-dinleme` ve `POST /sistem-olay-dinleme`. İkisi de **202 Accepted** dönmeli.

## 3. İzin türleri
| Kod | İzin | Kural |
|---|---|---|
| 01 | Temel Hesap Bilgisi | 07 seçilmediyse zorunlu |
| 02 | Ayrıntılı Hesap Bilgisi | opsiyonel |
| 03 | Bakiye Bilgisi | opsiyonel |
| 04 | Temel İşlem Bilgisi | opsiyonel |
| 05 | Ayrıntılı İşlem Bilgisi | 04'ü gerektirir |
| 06 | Anlık Bakiye Bildirimi | 03 + olay aboneliği gerekir |
| 07 | Temel Kart Bilgisi | 01 seçilmediyse zorunlu |
| 08 | Detaylı Kart Bilgisi | opsiyonel |
| 09 | Ayrıntılı Kart İşlem Bilgisi | opsiyonel |

## 4. Rıza (consent) akışı
0. Kullanıcı uygulamada "Banka bağla" der.
1. YÖS `POST /hesap-bilgisi-rizasi` çağırır. Durum: **B (Yetki Bekleniyor)**.
2. GKD yapılır, kullanıcı bankada hangi hesap ve kartları paylaşacağını seçer. Durum: **Y (Yetkilendirildi)** ve yetKod üretilir.
3. `POST /erisim-belirteci` (yetTip=yet_kod) çağrılır. Durum: **K (Yetki Kullanıldı)**.
4. Veri çekilir. Süre dolunca **S (Sonlandırıldı)**, iptal edilirse **I (İptal)** + rizaIptDtyKod.

Kurallar:
- **Bir ÖHK'nın bir YÖS için bir HHS'de yalnızca 1 aktif HBH rızası olabilir.** Yeni rıza eskisini iptal eder (kod 01). Güncelleme için kod 15 kullanılır.
- Süre: bireysel en fazla 6 ay, kurumsal en fazla 12 ay, en az 1 gün.
- İptal edilmiş veya sonlanmış rıza 6 ay boyunca sorgulanabilir.

Rıza isteği iskeleti:
```json
{
  "katilimciBlg": { "hhsKod": "0062", "yosKod": "9999" },
  "gkd": { "yetYntm": "Y", "yonAdr": "https://app.ornek.com/gkd/callback" },
  "kmlk": { "kmlkTur": "K", "kmlkVrs": "12345678901", "ohkTur": "B" },
  "hspBlg": {
    "iznBlg": {
      "iznTur": ["01","02","03","04","05","06","07","08","09"],
      "erisimIzniSonTrh": "2027-03-29T00:00:00+03:00",
      "hesapIslemBslZmn": "2025-09-29T00:00:00+03:00",
      "hesapIslemBtsZmn": "2027-03-29T00:00:00+03:00"
    }
  }
}
```
Yanıt kökü: `rzBlg {rizaNo, olusZmn, gnclZmn, rizaDrm, rizaIptDtyKod}`, `kmlk`, `katilimciBlg`, `gkd {yetYntm, yonAdr, hhsYonAdr, yetTmmZmn, ayrikGkd}`, `hspBlg {iznBlg, ayrBlg}`.

Rıza iptal detay kodları: 01 yeni rıza · 02 kullanıcı/HHS · 03 kullanıcı/YÖS · 04–06 süre aşımı · 07 mükerrer çağrı · 08 rizaNo–TCKN uyuşmazlığı · 09 uygun ürün yok · 10 HHS kanalı kapalı · 11 hesap yetki sorunu · 12 HHS kontrollerini aşamadı · 13 kullanıcı vazgeçti · 14 fraud şüphesi · 15 hesap rıza güncelleme · 99 diğer.

## 5. GKD (Güçlü Kimlik Doğrulama)
- **Yönlendirmeli (yetYntm="Y"):** Kullanıcı `hhsYonAdr` adresine gider, bankada doğrulama yapar ve `yonAdr` ile bize döner (yetKod gelir). CSRF koruması için **drmKod** (state) kullanılır. Mobilde universal link ile banka uygulaması açılır.
- **Ayrık (yetYntm="A"):** `ayrikGkd {ohkTanimTip: TCKN|MNO|YKN|PNO|GSM|IBAN, ohkTanimDeger}`. Banka kendi uygulamasına push gönderir. Sonuç `AYRIK_GKD_BASARILI` / `AYRIK_GKD_BASARISIZ` olayıyla gelir. 5 dk içinde sonuç gelmezse iptal olur.

## 6. Erişim belirteci
- `POST /erisim-belirteci` → `{rizaNo, rizaTip:"H", yetTip:"yet_kod"|"yenileme_belirteci", yetKod | yenilemeBelirteci}`
- Yanıt: `{erisimBelirteci, gecerlilikSuresi, yenilemeBelirteci, yenilemeBelirteciGecerlilikSuresi}` (süreler saniye cinsinden)
- HBH için access token 1–30 gün geçerli, refresh token rıza bitene kadar geçerli.
- Sonraki isteklerde `X-Access-Token` başlığı kullanılır.
- İstek ve yanıtlar JWS ile imzalanır (`X-JWS-Signature`).
- Ortak başlıklar: `X-Request-ID, X-Group-ID, X-ASPSP-Code (HHS), X-TPP-Code (YÖS), PSU-Initiated (E=kullanıcı tetikli, H=sistem), X-JWS-Signature, X-Access-Token`.

## 7. Sorgu limitleri (mimariyi belirleyen kısım)
| | Bireysel | Kurumsal |
|---|---|---|
| Kullanıcı tetikli sorgu (PSU-Initiated=E) tarih penceresi | 1 ay | 1 hafta |
| Sistem tetikli sorgu (PSU-Initiated=H) tarih penceresi | 24 saat | 24 saat |
| Sistem tetikli sorgu sıklığı | günde 4 başarılı | saatte 12 başarılı |
| Geçmişe dönük erişim | en fazla 12 ay | en fazla 12 ay |

Limit aşılırsa `TR.OHVPS.Connection.ExceededRate` hatası döner.
İşlem sorgu parametreleri: `hesapIslemBslTrh, hesapIslemBtsTrh, minIslTtr, mksIslTtr, brcAlc, syfKytSayi, syfNo, srlmKrtr, srlmYon`.

## 8. Veri modelleri
**Hesap:** `hspTml {hspRef, hspNo(IBAN), hspShb, subeAdi, kisaAd, prBrm, hspTur(B|T), hspTip(VADESIZ|KREDILI_MEVDUAT_HESABI|POS|CEK|YATIRIM), hspUrunAdi, hspDrm(AKTIF|PASIF|KAPALI)}`, `hspDty {hspAclsTrh, ...}`

**Bakiye:** `{hspRef, bky {bkyTtr (negatif olabilir), blkTtr, prBrm, bkyZmn}, krdHsp {kulKrdTtr, krdDhlGstr: "0"|"1"}}`

**İşlem:** `islTml {islNo, refNo, islTtr, prBrm, islGrckZaman, kanal, brcAlc(B|A|N), islTur, islAmc, ohkMsj, odmStmNo, islAcklm, krsTrf ...}`, karşı taraf bilgisi 05 izniyle gelir.
- islTur: HAVALE, EFT, FAST, PARA_YATIRMA, PARA_CEKME, YABANCI_PARA_HAVALE, YATIRIM_HESABINA/HESABINDAN_AKTARIM, KURUM_FATURA_ODEMESI, CEK, SENET, SIGORTA_ODEMESI, UCRET_KOMISYON_FAIZ, SGK_ODEMESI, VERGI_ODEMESI, DOVIZ_ALIM, DOVIZ_SATIM, KREDI_ODEMESI, KREDI_KULLANIM, KK_ODEMESI, KK_NAKIT_AVANS, SANS_OYUNU, UYE_ISYERI_ISLEMLERI, HGS_OGS_ISLEMLERI, DOGRUDAN_BORCLANDIRMA_SISTEMI, DIGER
- islAmc: 01 konut kirası · 02 işyeri kirası · 03 diğer kira · 04 e-ticaret · 05 maaş/harcırah/prim · 06 ticari · 07 bireysel · 08 yatırım · 09 finansal · 10 eğitim · 11 aidat · 12 gayrimenkul alımı · 13 taşıt alımı · 14 borç verme/ödeme · 15 hediye/bağış · 16 vergi/harç · 17 tazminat/sigorta · 18 avukat/danışman · 19 sağlık · 20 kripto · 21 şans oyunu/bahis · 22 eğlence/sosyal medya · 99 diğer (12–22 arası kodlar 02.04.2026'dan itibaren geçerli)

**Kart:** `kartRef, kartTipi(B banka|K kredi|P prepaid), altKartTipi(A asıl|E ek|S sanal), kartFormu(F|D), kartTuru(B|T), kartStatu(A|P|I), kartSema(T|M|V|U|A|J), kartUrunAdi, ...`

**Kart detay (08):** hesap kesim tarihi, son ödeme tarihi, dönem borcu, asgari ödeme, limit ve kullanılan limit, nakit limit. ⚠ Alan adları özet araçtan geldi, doğrulanmalı.

**Kart hareketleri (09):** işlem no/tarih/tutar/para birimi, taksit sayısı, döviz bilgisi, üye işyeri, açıklama. ⚠ Alan adları doğrulanmalı.

## 9. Olay bildirim
- Olay tipleri: `KAYNAK_GUNCELLENDI`, `AYRIK_GKD_BASARILI`, `AYRIK_GKD_BASARISIZ`, `HHS_YOS_GUNCELLENDI`
- Kaynak tipleri (HBH ile ilgili olanlar): `HESAP_BILGISI_RIZASI`, `BAKIYE`, `KART_BILGILERI`
- Bakiye bildirimi en geç 10 dk içinde gelir. Başarısız gönderim yeniden denenir (standart olaylarda 30 dk×3, kart ve GKD olaylarında 1 dk×3).

## 10. Başlıca hata kodları
`InvalidASPSP, InvalidTPP, ConsentAlreadyExists, IncorrectPermissionType, ConsentRevoked, ExceededRate, InvalidStartEndTime` (hepsinin öneki `TR.OHVPS.*`)
