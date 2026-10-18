namespace FinCopilot.Infrastructure.Hhs;

// ÖHVPS v2.0.0 JSON şemaları. Özellik adları camelCase'e çevrilince spesifikasyondaki adlarla birebir aynı olur.
// Sadece kullandığımız alanlar var; bilinmeyen alanlar okunurken yok sayılır.

internal sealed record KatilimciBlg(string HhsKod, string YosKod);

internal sealed record Gkd(string YetYntm, string YonAdr, string? HhsYonAdr = null);

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
