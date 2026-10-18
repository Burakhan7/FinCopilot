namespace FinCopilot.Infrastructure.Hhs;

// ÖHVPS v2.0.0 JSON şemaları. Özellik adları camelCase'e çevrilince spesifikasyondaki adlarla birebir aynı olur.
// Sadece kullandığımız alanlar var; bilinmeyen alanlar okunurken yok sayılır.

internal sealed record KatilimciBlg(string HhsKod, string YosKod);

/// <param name="YetKod">Mock HHS: ayrık GKD onaylanınca rıza sorgusunda döner.</param>
internal sealed record Gkd(string YetYntm, string YonAdr, string? HhsYonAdr = null, AyrikGkd? AyrikGkd = null, string? YetKod = null);

/// <param name="OhkTanimTip">TCKN | MNO | YKN | PNO | GSM | IBAN</param>
internal sealed record AyrikGkd(string OhkTanimTip, string OhkTanimDeger);

internal sealed record Kmlk(string KmlkTur, string KmlkVrs, string OhkTur);

internal sealed record IznBlg(
    IReadOnlyList<string> IznTur,
    DateTimeOffset ErisimIzniSonTrh,
    DateTimeOffset? HesapIslemBslZmn,
    DateTimeOffset? HesapIslemBtsZmn);

internal sealed record HspBlg(IznBlg IznBlg);

internal sealed record HesapBilgisiRizasiIstegi(KatilimciBlg KatilimciBlg, Gkd Gkd, Kmlk Kmlk, HspBlg HspBlg);

internal sealed record RzBlg(string RizaNo, string RizaDrm, string? RizaIptDtyKod);

internal sealed record HesapBilgisiRizasi(RzBlg RzBlg, Gkd Gkd, HspBlg HspBlg);

internal sealed record ErisimBelirteciIstegi(
    string RizaNo,
    string RizaTip,
    string YetTip,
    string? YetKod = null,
    string? YenilemeBelirteci = null);

internal sealed record ErisimBelirteciYaniti(
    string ErisimBelirteci,
    int GecerlilikSuresi,
    string YenilemeBelirteci,
    int YenilemeBelirteciGecerlilikSuresi);

internal sealed record OhvpsHata(int? HttpCode, string? ErrorCode, string? MoreInformationTr);

// ---- Olay aboneliği (/olay-abonelik)

internal sealed record AbonelikTipi(string OlayTipi, string KaynakTipi);

internal sealed record OlayAbonelikIstegi(KatilimciBlg KatilimciBlg, IReadOnlyList<AbonelikTipi> AbonelikTipleri);

internal sealed record OlayAbonelik(string OlayAbonelikNo, IReadOnlyList<AbonelikTipi> AbonelikTipleri);

// ---- Veri uçları. Tutarlar ÖHVPS'te string ("1234.50"); serileştirici string'den decimal okur.

internal sealed record HspTml(
    string HspRef, string? HspNo, string? HspShb, string? SubeAdi, string? KisaAd, string PrBrm,
    string? HspTur, string? HspTip, string? HspUrunAdi, string? HspDrm);

internal sealed record HspDty(DateTimeOffset? HspAclsTrh);

internal sealed record Hesap(HspTml HspTml, HspDty? HspDty);

internal sealed record Bky(decimal BkyTtr, decimal? BlkTtr, string PrBrm, DateTimeOffset BkyZmn);

internal sealed record KrdHsp(decimal? KulKrdTtr, string? KrdDhlGstr);

internal sealed record Bakiye(string HspRef, Bky Bky, KrdHsp? KrdHsp);

internal sealed record IslTml(
    string IslNo, string? RefNo, decimal IslTtr, string PrBrm, DateTimeOffset IslGrckZaman, string? Kanal,
    string BrcAlc, string? IslTur, string? IslAmc, string? IslAcklm, string? OhkMsj);

/// <summary>05 (ayrıntılı işlem) izniyle gelen karşı taraf bilgisi.</summary>
internal sealed record IslDty(string? OdmKsnm, string? OdmHsbNo);

internal sealed record Islem(IslTml IslTml, IslDty? IslDty);

/// <summary>Mock HHS'deki dizi adı "isller".</summary>
internal sealed record IslemSayfasi(string HspRef, IReadOnlyList<Islem> Isller);

internal sealed record Kart(
    string KartRef, string? KartSonNo, string KartTipi, string? AltKartTipi, string? KartFormu,
    string? KartTuru, string? KartStatu, string? KartSema, string? KartUrunAdi);

internal sealed record KartDetay(
    string KartRef, DateTimeOffset? HesapKesimTarihi, DateTimeOffset? SonOdemeTarihi, decimal DonemBorcu,
    decimal AsgariOdeme, decimal? LimitTutari, decimal? KullanilanLimit, string PrBrm);

internal sealed record DovizBilgisi(decimal OrijinalTutar, string OrijinalParaBirimi, decimal Kur);

internal sealed record KartHareketi(
    string IslemNo, DateTimeOffset IslemTarihi, decimal IslemTutari, string IslemParaBirimi, string BrcAlc,
    string? IslemTuru, string? IslemAciklama, string? UyeIsyeri, string? ReferansNo, int? TaksitSayisi,
    DovizBilgisi? DovizBilgisi);

internal sealed record KartHareketiSayfasi(string KartRef, IReadOnlyList<KartHareketi> KartHareketleri);
