using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FinCopilot.Application.Abstractions;
using FinCopilot.Application.Consents;
using FinCopilot.Infrastructure.Hhs;
using FinCopilot.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace FinCopilot.UnitTests.Consents;

/// <summary>ÖHVPS sözleşmesi: alan adları, başlıklar, hata eşlemesi. Gerçek Mock HHS ile uçtan uca test 6. adımda.</summary>
public class MockHhsClientTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static (MockHhsClient, StubHandler) Create(HttpStatusCode status, string body)
    {
        var handler = new StubHandler(status, body);
        var options = Options.Create(new HhsOptions
        {
            YosKod = "8001",
            Banks = [new HhsBankOptions { HhsKod = "9001", Name = "Anadolu Bank", ApiBaseUrl = "http://hhs.test/v2" }],
        });
        return (new MockHhsClient(new HttpClient(handler), options), handler);
    }

    private const string ConsentResponse = """
        {"rzBlg":{"rizaNo":"r-1","olusZmn":"2026-10-18T10:00:00+03:00","gnclZmn":"2026-10-18T10:00:00+03:00","rizaDrm":"B"},
         "kmlk":{"kmlkTur":"K","kmlkVrs":"10000000214","ohkTur":"B"},
         "katilimciBlg":{"hhsKod":"9001","yosKod":"8001"},
         "gkd":{"yetYntm":"Y","yonAdr":"fincopilot://gkd-donus","hhsYonAdr":"http://hhs.test/gkd/r-1"},
         "hspBlg":{"iznBlg":{"iznTur":["01","03"],"erisimIzniSonTrh":"2027-04-16T10:00:00+03:00",
                   "hesapIslemBslZmn":"2025-10-18T10:00:00+03:00","hesapIslemBtsZmn":"2027-04-16T10:00:00+03:00"}}}
        """;

    [Fact]
    public async Task Create_consent_sends_ohvps_body_and_headers()
    {
        var (sut, handler) = Create(HttpStatusCode.Created, ConsentResponse);
        var son = new DateTimeOffset(2027, 4, 16, 7, 0, 0, TimeSpan.Zero);

        var result = await sut.CreateConsentAsync(new HhsConsentRequest(
            "9001", "10000000214", ["01", "03"], son, son.AddDays(-545), son, "fincopilot://gkd-donus"), default);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("http://hhs.test/v2/hesap-bilgisi-rizasi", handler.Request.RequestUri!.ToString());
        Assert.Equal("9001", handler.Request.Headers.GetValues("X-ASPSP-Code").Single());
        Assert.Equal("8001", handler.Request.Headers.GetValues("X-TPP-Code").Single());
        Assert.True(Guid.TryParse(handler.Request.Headers.GetValues("X-Request-ID").Single(), out _));

        var body = JsonNode.Parse(handler.RequestBody!)!;
        Assert.Equal("9001", (string?)body["katilimciBlg"]!["hhsKod"]);
        Assert.Equal("8001", (string?)body["katilimciBlg"]!["yosKod"]);
        Assert.Equal("Y", (string?)body["gkd"]!["yetYntm"]);
        Assert.Equal("fincopilot://gkd-donus", (string?)body["gkd"]!["yonAdr"]);
        Assert.Null(body["gkd"]!["hhsYonAdr"]);
        Assert.Equal("K", (string?)body["kmlk"]!["kmlkTur"]);
        Assert.Equal("10000000214", (string?)body["kmlk"]!["kmlkVrs"]);
        Assert.Equal("B", (string?)body["kmlk"]!["ohkTur"]);
        Assert.Equal(["01", "03"], body["hspBlg"]!["iznBlg"]!["iznTur"]!.AsArray().Select(x => (string)x!));
        Assert.NotNull(body["hspBlg"]!["iznBlg"]!["erisimIzniSonTrh"]);

        Assert.Equal("r-1", result.RizaNo);
        Assert.Equal("B", result.RizaDrm);
        Assert.Equal("http://hhs.test/gkd/r-1", result.HhsYonAdr);
        Assert.Equal(new DateTimeOffset(2027, 4, 16, 7, 0, 0, TimeSpan.Zero), result.ErisimIzniSonTrh);
    }

    [Fact]
    public async Task Token_exchange_maps_lifetimes()
    {
        var (sut, handler) = Create(HttpStatusCode.OK,
            """{"erisimBelirteci":"a","gecerlilikSuresi":86400,"yenilemeBelirteci":"r","yenilemeBelirteciGecerlilikSuresi":3600}""");

        var token = await sut.ExchangeAuthCodeAsync("9001", "r-1", "yet-123", default);

        var body = JsonNode.Parse(handler.RequestBody!)!;
        Assert.Equal("r-1", (string?)body["rizaNo"]);
        Assert.Equal("H", (string?)body["rizaTip"]);
        Assert.Equal("yet_kod", (string?)body["yetTip"]);
        Assert.Equal("yet-123", (string?)body["yetKod"]);
        Assert.Null(body["yenilemeBelirteci"]);
        Assert.Equal(new HhsToken("a", TimeSpan.FromDays(1), "r", TimeSpan.FromHours(1)), token);
    }

    [Fact]
    public async Task Revoke_sends_access_token_and_accepts_204()
    {
        var (sut, handler) = Create(HttpStatusCode.NoContent, "");

        await sut.RevokeConsentAsync("9001", "r-1", "erisim", default);

        Assert.Equal(HttpMethod.Delete, handler.Request!.Method);
        Assert.Equal("erisim", handler.Request.Headers.GetValues("X-Access-Token").Single());
    }

    [Fact]
    public async Task Ohvps_error_becomes_hhs_exception()
    {
        var (sut, _) = Create(HttpStatusCode.Forbidden,
            """{"path":"/x","httpCode":403,"errorCode":"TR.OHVPS.Resource.ConsentRevoked","moreInformationTr":"Rıza iptal edilmiş."}""");

        var ex = await Assert.ThrowsAsync<HhsException>(() => sut.RevokeConsentAsync("9001", "r-1", null, default));

        Assert.Equal(403, ex.StatusCode);
        Assert.True(ex.IsConsentRevoked);
        Assert.Equal("Rıza iptal edilmiş.", ex.Message);
    }

    [Fact]
    public async Task Unknown_bank_is_rejected_without_a_request()
    {
        var (sut, handler) = Create(HttpStatusCode.OK, "{}");

        await Assert.ThrowsAsync<HhsException>(() => sut.GetConsentAsync("9999", "r-1", default));
        Assert.Null(handler.Request);
    }

    [Fact]
    public void Tckn_hash_is_keyed()
    {
        var a = new HmacTcknHasher(Options.Create(new SecurityOptions { TcknHashKey = new string('a', 32) }));
        var b = new HmacTcknHasher(Options.Create(new SecurityOptions { TcknHashKey = new string('b', 32) }));

        Assert.Equal(a.Hash("10000000214"), a.Hash("10000000214"));
        Assert.NotEqual(a.Hash("10000000214"), b.Hash("10000000214"));
        Assert.Equal(64, a.Hash("10000000214").Length);
        Assert.Throws<InvalidOperationException>(() =>
            new HmacTcknHasher(Options.Create(new SecurityOptions { TcknHashKey = "kisa" })));
    }

    [Fact]
    public async Task Decoupled_consent_sends_ayrik_gkd_and_reads_yet_kod()
    {
        // mock-hhs: ayrık GKD onaylanınca rıza sorgusunda gkd.yetKod döner, hhsYonAdr dönmez.
        var (sut, handler) = Create(HttpStatusCode.Created, ConsentResponse
            .Replace("\"yetYntm\":\"Y\"", "\"yetYntm\":\"A\"")
            .Replace("\"hhsYonAdr\":\"http://hhs.test/gkd/r-1\"", "\"ayrikGkd\":{\"ohkTanimTip\":\"TCKN\",\"ohkTanimDeger\":\"10000000214\"},\"yetKod\":\"yet-9\"")
            .Replace("\"rizaDrm\":\"B\"", "\"rizaDrm\":\"Y\""));
        var son = DateTimeOffset.UtcNow.AddDays(180);

        var result = await sut.CreateConsentAsync(new HhsConsentRequest(
            "9001", "10000000214", ["01"], son, son.AddDays(-545), son, "fincopilot://gkd-donus", Decoupled: true), default);

        var gkd = JsonNode.Parse(handler.RequestBody!)!["gkd"]!;
        Assert.Equal("A", (string?)gkd["yetYntm"]);
        Assert.Equal("TCKN", (string?)gkd["ayrikGkd"]!["ohkTanimTip"]);
        Assert.Equal("10000000214", (string?)gkd["ayrikGkd"]!["ohkTanimDeger"]);
        Assert.Null(gkd["yetKod"]);
        Assert.Equal("yet-9", result.YetKod);
        Assert.Null(result.HhsYonAdr);
    }

    [Fact]
    public async Task Subscription_create_sends_types_and_missing_subscription_is_null()
    {
        var (sut, handler) = Create(HttpStatusCode.Created, """
            {"olayAbonelikNo":"s-1","olusturmaZamani":"2026-10-18T10:00:00+03:00","guncellemeZamani":"2026-10-18T10:00:00+03:00",
             "katilimciBlg":{"hhsKod":"9001","yosKod":"8001"},
             "abonelikTipleri":[{"olayTipi":"KAYNAK_GUNCELLENDI","kaynakTipi":"BAKIYE"}]}
            """);

        var sub = await sut.CreateSubscriptionAsync("9001", [new HhsEventType("KAYNAK_GUNCELLENDI", "BAKIYE")], default);

        Assert.Equal("http://hhs.test/v2/olay-abonelik", handler.Request!.RequestUri!.ToString());
        var body = JsonNode.Parse(handler.RequestBody!)!;
        Assert.Equal("8001", (string?)body["katilimciBlg"]!["yosKod"]);
        Assert.Equal("BAKIYE", (string?)body["abonelikTipleri"]![0]!["kaynakTipi"]);
        Assert.Equal(new HhsEventType("KAYNAK_GUNCELLENDI", "BAKIYE"), Assert.Single(sub.Types));
        Assert.Equal("s-1", sub.OlayAbonelikNo);

        var (missing, _) = Create(HttpStatusCode.NotFound,
            """{"httpCode":404,"errorCode":"TR.OHVPS.Resource.NotFound","moreInformationTr":"Olay aboneliği bulunamadı."}""");
        Assert.Null(await missing.GetSubscriptionAsync("9001", default));
    }

    [Fact]
    public async Task Single_account_balance_uses_its_own_path()
    {
        var (sut, handler) = Create(HttpStatusCode.OK,
            """{"hspRef":"A1","bky":{"bkyTtr":"36000.50","blkTtr":"0.00","prBrm":"TRY","bkyZmn":"2026-10-18T10:00:00+03:00"}}""");

        var b = await sut.GetBalanceAsync(new HhsAccess("9001", "t", false), "A1", default);

        Assert.Equal("/v2/hesaplar/A1/bakiye", handler.Request!.RequestUri!.AbsolutePath);
        Assert.Equal("H", handler.Request.Headers.GetValues("PSU-Initiated").Single());
        Assert.Equal(36000.50m, b.BkyTtr);
    }

    [Fact]
    public void Serializer_uses_ohvps_names() =>
        Assert.Contains("\"erisimBelirteci\"", JsonSerializer.Serialize(
            new { ErisimBelirteci = "x" }, MockHhsClient.Json));
}
