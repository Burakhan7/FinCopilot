using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinCopilot.Application.Abstractions;
using FinCopilot.Application.Consents;
using Microsoft.Extensions.Options;

namespace FinCopilot.Infrastructure.Hhs;

/// <summary>
/// Mock HHS (mock-hhs/) için ÖHVPS v2.0.0 istemcisi. Her banka kendi adresinde (Hhs:Banks:*:ApiBaseUrl).
/// İmzalama (X-JWS-Signature) mock'ta zorunlu değil, gönderilmez.
/// </summary>
internal sealed class MockHhsClient(HttpClient http, IOptions<HhsOptions> options) : IHhsClient
{
    public const string HttpClientName = "hhs";

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // ÖHVPS tutarları string ("1234.50") taşır.
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly HhsOptions _o = options.Value;

    // ------------------------------------------------------------------ rıza ve token

    public async Task<HhsConsent> CreateConsentAsync(HhsConsentRequest r, CancellationToken ct)
    {
        var gkd = r.Decoupled
            ? new Gkd("A", r.YonAdr, AyrikGkd: new AyrikGkd("TCKN", r.Tckn))
            : new Gkd("Y", r.YonAdr);
        var body = new HesapBilgisiRizasiIstegi(
            new KatilimciBlg(r.HhsKod, _o.YosKod),
            gkd,
            new Kmlk("K", r.Tckn, "B"),
            new HspBlg(new IznBlg(r.IzinTurleri, r.ErisimIzniSonTrh, r.HesapIslemBslZmn, r.HesapIslemBtsZmn)));

        return Map((await SendAsync<HesapBilgisiRizasi>(new(r.HhsKod, HttpMethod.Post, "hesap-bilgisi-rizasi") { Body = body }, ct))!);
    }

    public async Task<HhsConsent> GetConsentAsync(string hhsKod, string rizaNo, CancellationToken ct) =>
        Map((await SendAsync<HesapBilgisiRizasi>(new(hhsKod, HttpMethod.Get, $"hesap-bilgisi-rizasi/{Esc(rizaNo)}"), ct))!);

    public Task RevokeConsentAsync(string hhsKod, string rizaNo, string? accessToken, CancellationToken ct) =>
        SendAsync<object>(new(hhsKod, HttpMethod.Delete, $"hesap-bilgisi-rizasi/{Esc(rizaNo)}") { AccessToken = accessToken }, ct);

    public Task<HhsToken> ExchangeAuthCodeAsync(string hhsKod, string rizaNo, string yetKod, CancellationToken ct) =>
        TokenAsync(hhsKod, new ErisimBelirteciIstegi(rizaNo, "H", "yet_kod", YetKod: yetKod), ct);

    public Task<HhsToken> RefreshAccessTokenAsync(string hhsKod, string rizaNo, string refreshToken, CancellationToken ct) =>
        TokenAsync(hhsKod, new ErisimBelirteciIstegi(rizaNo, "H", "yenileme_belirteci", YenilemeBelirteci: refreshToken), ct);

    private async Task<HhsToken> TokenAsync(string hhsKod, ErisimBelirteciIstegi body, CancellationToken ct)
    {
        var res = (await SendAsync<ErisimBelirteciYaniti>(new(hhsKod, HttpMethod.Post, "erisim-belirteci") { Body = body }, ct))!;
        return new HhsToken(
            res.ErisimBelirteci, TimeSpan.FromSeconds(res.GecerlilikSuresi),
            res.YenilemeBelirteci, TimeSpan.FromSeconds(res.YenilemeBelirteciGecerlilikSuresi));
    }

    // ------------------------------------------------------------------ veri uçları

    public async Task<IReadOnlyList<HhsAccount>> GetAccountsAsync(HhsAccess access, CancellationToken ct) =>
        (await SendAsync<List<Hesap>>(Data(access, "hesaplar"), ct))!.Select(h => new HhsAccount(
            h.HspTml.HspRef, h.HspTml.HspNo, h.HspTml.HspShb, h.HspTml.SubeAdi, h.HspTml.KisaAd, h.HspTml.PrBrm,
            h.HspTml.HspTur, h.HspTml.HspTip, h.HspTml.HspUrunAdi, h.HspTml.HspDrm, h.HspDty?.HspAclsTrh)).ToList();

    public async Task<IReadOnlyList<HhsBalance>> GetBalancesAsync(HhsAccess access, CancellationToken ct) =>
        (await SendAsync<List<Bakiye>>(Data(access, "bakiye"), ct))!.Select(b => new HhsBalance(
            b.HspRef, b.Bky.BkyTtr, b.Bky.BlkTtr, b.Bky.PrBrm, b.Bky.BkyZmn, b.KrdHsp?.KulKrdTtr, b.KrdHsp?.KrdDhlGstr)).ToList();

    public async Task<HhsBalance> GetBalanceAsync(HhsAccess access, string hspRef, CancellationToken ct)
    {
        var b = (await SendAsync<Bakiye>(Data(access, $"hesaplar/{Esc(hspRef)}/bakiye"), ct))!;
        return new HhsBalance(b.HspRef, b.Bky.BkyTtr, b.Bky.BlkTtr, b.Bky.PrBrm, b.Bky.BkyZmn, b.KrdHsp?.KulKrdTtr, b.KrdHsp?.KrdDhlGstr);
    }

    public async Task<HhsPage<HhsTransaction>> GetTransactionsAsync(
        HhsAccess access, string hspRef, HhsWindow window, int page, int pageSize, CancellationToken ct)
    {
        var (body, total, pages) = await SendPagedAsync<IslemSayfasi>(
            Data(access, $"hesaplar/{Esc(hspRef)}/islemler?{WindowQuery(window, page, pageSize)}"), ct);

        return new HhsPage<HhsTransaction>(body.Isller.Select(i => new HhsTransaction(
            i.IslTml.IslNo, i.IslTml.RefNo, i.IslTml.IslTtr, i.IslTml.PrBrm, i.IslTml.IslGrckZaman, i.IslTml.Kanal,
            i.IslTml.BrcAlc, i.IslTml.IslTur, i.IslTml.IslAmc, i.IslTml.IslAcklm, i.IslTml.OhkMsj,
            i.IslDty?.OdmKsnm, i.IslDty?.OdmHsbNo)).ToList(), total, pages);
    }

    public async Task<IReadOnlyList<HhsCard>> GetCardsAsync(HhsAccess access, CancellationToken ct) =>
        (await SendAsync<List<Kart>>(Data(access, "kartlar"), ct))!.Select(k => new HhsCard(
            k.KartRef, k.KartSonNo, k.KartTipi, k.AltKartTipi, k.KartFormu, k.KartTuru, k.KartStatu, k.KartSema,
            k.KartUrunAdi)).ToList();

    public async Task<HhsCardDetail> GetCardDetailAsync(HhsAccess access, string kartRef, CancellationToken ct)
    {
        var d = (await SendAsync<KartDetay>(Data(access, $"kartlar/{Esc(kartRef)}/kart-detay"), ct))!;
        return new HhsCardDetail(
            d.KartRef, LocalDate(d.HesapKesimTarihi), LocalDate(d.SonOdemeTarihi), d.DonemBorcu, d.AsgariOdeme,
            d.LimitTutari, d.KullanilanLimit, d.PrBrm);
    }

    public async Task<HhsPage<HhsCardTransaction>> GetCardTransactionsAsync(
        HhsAccess access, string kartRef, HhsWindow window, int page, int pageSize, CancellationToken ct)
    {
        var (body, total, pages) = await SendPagedAsync<KartHareketiSayfasi>(
            Data(access, $"kartlar/{Esc(kartRef)}/kart-hareketleri?{WindowQuery(window, page, pageSize)}"), ct);

        return new HhsPage<HhsCardTransaction>(body.KartHareketleri.Select(m => new HhsCardTransaction(
            m.IslemNo, m.IslemTarihi, m.IslemTutari, m.IslemParaBirimi, m.BrcAlc, m.IslemTuru, m.IslemAciklama,
            m.UyeIsyeri, m.ReferansNo, m.TaksitSayisi,
            m.DovizBilgisi is { } fx ? new HhsFx(fx.OrijinalTutar, fx.OrijinalParaBirimi, fx.Kur) : null)).ToList(), total, pages);
    }

    // ------------------------------------------------------------------ olay aboneliği

    public async Task<HhsSubscription?> GetSubscriptionAsync(string hhsKod, CancellationToken ct)
    {
        try
        {
            return Map((await SendAsync<OlayAbonelik>(new(hhsKod, HttpMethod.Get, "olay-abonelik"), ct))!);
        }
        catch (HhsException e) when (e.StatusCode == 404)
        {
            return null;
        }
    }

    public async Task<HhsSubscription> CreateSubscriptionAsync(string hhsKod, IReadOnlyList<HhsEventType> types, CancellationToken ct) =>
        Map((await SendAsync<OlayAbonelik>(new(hhsKod, HttpMethod.Post, "olay-abonelik") { Body = SubscriptionBody(hhsKod, types) }, ct))!);

    public async Task<HhsSubscription> UpdateSubscriptionAsync(string hhsKod, string olayAbonelikNo, IReadOnlyList<HhsEventType> types, CancellationToken ct) =>
        Map((await SendAsync<OlayAbonelik>(
            new(hhsKod, HttpMethod.Put, $"olay-abonelik/{Esc(olayAbonelikNo)}") { Body = SubscriptionBody(hhsKod, types) }, ct))!);

    private OlayAbonelikIstegi SubscriptionBody(string hhsKod, IReadOnlyList<HhsEventType> types) =>
        new(new KatilimciBlg(hhsKod, _o.YosKod), types.Select(t => new AbonelikTipi(t.OlayTipi, t.KaynakTipi)).ToList());

    private static HhsSubscription Map(OlayAbonelik a) =>
        new(a.OlayAbonelikNo, a.AbonelikTipleri.Select(t => new HhsEventType(t.OlayTipi, t.KaynakTipi)).ToList());

    // ------------------------------------------------------------------ HTTP

    private sealed record Call(string HhsKod, HttpMethod Method, string Path)
    {
        public object? Body { get; init; }
        public string? AccessToken { get; init; }
        /// <summary>Veri uçlarında zorunlu: "E" kullanıcı tetikli, "H" sistem tetikli.</summary>
        public string? PsuInitiated { get; init; }
    }

    private static Call Data(HhsAccess a, string path) =>
        new(a.HhsKod, HttpMethod.Get, path) { AccessToken = a.AccessToken, PsuInitiated = a.PsuInitiated ? "E" : "H" };

    private async Task<T?> SendAsync<T>(Call call, CancellationToken ct)
    {
        using var res = await SendRawAsync(call, ct);
        return res.StatusCode == HttpStatusCode.NoContent ? default : await res.Content.ReadFromJsonAsync<T>(Json, ct);
    }

    private async Task<(T Body, int Total, int Pages)> SendPagedAsync<T>(Call call, CancellationToken ct)
    {
        using var res = await SendRawAsync(call, ct);
        var body = (await res.Content.ReadFromJsonAsync<T>(Json, ct))!;
        return (body, IntHeader(res, "X-Total-Count") ?? 0, IntHeader(res, "X-Total-Pages") ?? 1);
    }

    private async Task<HttpResponseMessage> SendRawAsync(Call call, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(call.Method, new Uri(BaseUri(call.HhsKod), call.Path));
        req.Headers.Add("X-Request-ID", Guid.NewGuid().ToString());
        req.Headers.Add("X-ASPSP-Code", call.HhsKod);
        req.Headers.Add("X-TPP-Code", _o.YosKod);
        if (call.AccessToken is not null)
            req.Headers.Add("X-Access-Token", call.AccessToken);
        if (call.PsuInitiated is not null)
            req.Headers.Add("PSU-Initiated", call.PsuInitiated);
        if (call.Body is not null)
            req.Content = JsonContent.Create(call.Body, call.Body.GetType(), options: Json);

        var res = await http.SendAsync(req, ct);
        if (res.IsSuccessStatusCode)
            return res;

        using (res)
            throw await ToExceptionAsync(res, ct);
    }

    private Uri BaseUri(string hhsKod)
    {
        var bank = _o.Banks.FirstOrDefault(b => b.HhsKod == hhsKod)
            ?? throw new HhsException(400, "TR.OHVPS.Connection.InvalidASPSP", $"Tanımsız banka: {hhsKod}");
        return new Uri(bank.ApiBaseUrl.TrimEnd('/') + "/");
    }

    private static async Task<HhsException> ToExceptionAsync(HttpResponseMessage res, CancellationToken ct)
    {
        OhvpsHata? hata = null;
        try { hata = await res.Content.ReadFromJsonAsync<OhvpsHata>(Json, ct); }
        catch (JsonException) { }
        return new HhsException((int)res.StatusCode, hata?.ErrorCode, hata?.MoreInformationTr ?? $"HHS {(int)res.StatusCode} döndü.");
    }

    // ------------------------------------------------------------------ yardımcılar

    private static string Esc(string s) => Uri.EscapeDataString(s);

    private static string WindowQuery(HhsWindow w, int page, int pageSize) =>
        $"hesapIslemBslTrh={Esc(Iso(w.Start))}&hesapIslemBtsTrh={Esc(Iso(w.End))}" +
        $"&syfNo={page.ToString(CultureInfo.InvariantCulture)}&syfKytSayi={pageSize.ToString(CultureInfo.InvariantCulture)}";

    private static string Iso(DateTimeOffset d) => d.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

    /// <summary>Tarih alanları gece yarısı TR saatiyle gelir ("2026-10-10T00:00:00+03:00"); takvim günü kendi ofsetinde alınır.</summary>
    private static DateOnly? LocalDate(DateTimeOffset? d) => d is { } v ? DateOnly.FromDateTime(v.DateTime) : null;

    private static int? IntHeader(HttpResponseMessage res, string name) =>
        res.Headers.TryGetValues(name, out var values) && int.TryParse(values.FirstOrDefault(), CultureInfo.InvariantCulture, out var n)
            ? n : null;

    private static HhsConsent Map(HesapBilgisiRizasi r) => new(
        r.RzBlg.RizaNo, r.RzBlg.RizaDrm, r.RzBlg.RizaIptDtyKod, r.Gkd.HhsYonAdr,
        r.HspBlg.IznBlg.IznTur, r.HspBlg.IznBlg.ErisimIzniSonTrh,
        r.HspBlg.IznBlg.HesapIslemBslZmn, r.HspBlg.IznBlg.HesapIslemBtsZmn, r.Gkd.YetKod);
}
