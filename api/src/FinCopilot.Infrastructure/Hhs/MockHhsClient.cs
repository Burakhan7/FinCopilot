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
    };

    private readonly HhsOptions _o = options.Value;

    public async Task<HhsConsent> CreateConsentAsync(HhsConsentRequest r, CancellationToken ct)
    {
        var body = new HesapBilgisiRizasiIstegi(
            new KatilimciBlg(r.HhsKod, _o.YosKod),
            new Gkd("Y", r.YonAdr),
            new Kmlk("K", r.Tckn, "B"),
            new HspBlg(new IznBlg(r.IzinTurleri, r.ErisimIzniSonTrh, r.HesapIslemBslZmn, r.HesapIslemBtsZmn)));

        var res = await SendAsync<HesapBilgisiRizasi>(r.HhsKod, HttpMethod.Post, "hesap-bilgisi-rizasi", body, null, ct);
        return Map(res!);
    }

    public async Task<HhsConsent> GetConsentAsync(string hhsKod, string rizaNo, CancellationToken ct)
    {
        var res = await SendAsync<HesapBilgisiRizasi>(hhsKod, HttpMethod.Get, $"hesap-bilgisi-rizasi/{Uri.EscapeDataString(rizaNo)}", null, null, ct);
        return Map(res!);
    }

    public Task RevokeConsentAsync(string hhsKod, string rizaNo, string? accessToken, CancellationToken ct) =>
        SendAsync<object>(hhsKod, HttpMethod.Delete, $"hesap-bilgisi-rizasi/{Uri.EscapeDataString(rizaNo)}", null, accessToken, ct);

    public Task<HhsToken> ExchangeAuthCodeAsync(string hhsKod, string rizaNo, string yetKod, CancellationToken ct) =>
        TokenAsync(hhsKod, new ErisimBelirteciIstegi(rizaNo, "H", "yet_kod", YetKod: yetKod), ct);

    public Task<HhsToken> RefreshAccessTokenAsync(string hhsKod, string rizaNo, string refreshToken, CancellationToken ct) =>
        TokenAsync(hhsKod, new ErisimBelirteciIstegi(rizaNo, "H", "yenileme_belirteci", YenilemeBelirteci: refreshToken), ct);

    private async Task<HhsToken> TokenAsync(string hhsKod, ErisimBelirteciIstegi body, CancellationToken ct)
    {
        var res = (await SendAsync<ErisimBelirteciYaniti>(hhsKod, HttpMethod.Post, "erisim-belirteci", body, null, ct))!;
        return new HhsToken(
            res.ErisimBelirteci, TimeSpan.FromSeconds(res.GecerlilikSuresi),
            res.YenilemeBelirteci, TimeSpan.FromSeconds(res.YenilemeBelirteciGecerlilikSuresi));
    }

    private async Task<T?> SendAsync<T>(string hhsKod, HttpMethod method, string path, object? body, string? accessToken, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(method, new Uri(BaseUri(hhsKod), path));
        req.Headers.Add("X-Request-ID", Guid.NewGuid().ToString());
        req.Headers.Add("X-ASPSP-Code", hhsKod);
        req.Headers.Add("X-TPP-Code", _o.YosKod);
        if (accessToken is not null)
            req.Headers.Add("X-Access-Token", accessToken);
        if (body is not null)
            req.Content = JsonContent.Create(body, body.GetType(), options: Json);

        using var res = await http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
            throw await ToExceptionAsync(res, ct);
        if (res.StatusCode == HttpStatusCode.NoContent)
            return default;
        return await res.Content.ReadFromJsonAsync<T>(Json, ct);
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

    private static HhsConsent Map(HesapBilgisiRizasi r) => new(
        r.RzBlg.RizaNo, r.RzBlg.RizaDrm, r.RzBlg.RizaIptDtyKod, r.Gkd.HhsYonAdr,
        r.HspBlg.IznBlg.IznTur, r.HspBlg.IznBlg.ErisimIzniSonTrh,
        r.HspBlg.IznBlg.HesapIslemBslZmn, r.HspBlg.IznBlg.HesapIslemBtsZmn);
}
