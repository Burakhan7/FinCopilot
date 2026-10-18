using System.Net;
using System.Text;
using FinCopilot.Application.Abstractions;
using FinCopilot.Application.Consents;
using FinCopilot.Infrastructure.Hhs;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace FinCopilot.UnitTests.Hhs;

/// <summary>Veri uçlarının sözleşmesi: gerçek Mock HHS yanıtları (Fixtures/) doğru çözülüyor mu, istek doğru kuruluyor mu.</summary>
public class MockHhsDataTests
{
    private static readonly HhsAccess UserAccess = new("9002", "erisim-belirteci", PsuInitiated: true);
    private static readonly HhsAccess SystemAccess = UserAccess with { PsuInitiated = false };
    private static readonly TimeSpan Tr = TimeSpan.FromHours(3);

    private sealed class StubHandler(HttpStatusCode status, string body, Dictionary<string, string>? headers = null) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            var res = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            foreach (var (k, v) in headers ?? [])
                res.Headers.Add(k, v);
            return Task.FromResult(res);
        }
    }

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Hhs", "Fixtures", name + ".json"));

    private static (MockHhsClient, StubHandler) Client(string fixture, HttpStatusCode status = HttpStatusCode.OK, Dictionary<string, string>? headers = null)
    {
        var handler = new StubHandler(status, Fixture(fixture), headers);
        var options = Options.Create(new HhsOptions
        {
            YosKod = "8001",
            Banks = [new HhsBankOptions { HhsKod = "9002", Name = "Ege Bank", ApiBaseUrl = "http://hhs.test" }],
        });
        return (new MockHhsClient(new HttpClient(handler), options), handler);
    }

    private static void AssertDataHeaders(HttpRequestMessage req, string psu)
    {
        Assert.Equal("erisim-belirteci", req.Headers.GetValues("X-Access-Token").Single());
        Assert.Equal(psu, req.Headers.GetValues("PSU-Initiated").Single());
        Assert.Equal("9002", req.Headers.GetValues("X-ASPSP-Code").Single());
        Assert.Equal("8001", req.Headers.GetValues("X-TPP-Code").Single());
    }

    [Fact]
    public async Task Accounts()
    {
        var (sut, handler) = Client("hesaplar");

        var a = Assert.Single(await sut.GetAccountsAsync(UserAccess, default));

        Assert.Equal("http://hhs.test/hesaplar", handler.Request!.RequestUri!.ToString());
        AssertDataHeaders(handler.Request, "E");
        Assert.Equal("A9002-ZD-01", a.HspRef);
        Assert.Equal("TR250900205202796328220556", a.HspNo);
        Assert.Equal("ZEYNEP DEMIR", a.HspShb);
        Assert.Equal("ALSANCAK/IZMIR", a.SubeAdi);
        Assert.Equal("TRY", a.ParaBirimi);
        Assert.Equal("VADESIZ", a.HspTip);
        Assert.Equal("AKTIF", a.HspDrm);
        Assert.Equal(new DateTimeOffset(2018, 6, 11, 0, 0, 0, Tr), a.AcilisTrh);
    }

    [Fact]
    public async Task Balances_parse_string_amounts()
    {
        var (sut, handler) = Client("bakiye");

        var b = Assert.Single(await sut.GetBalancesAsync(SystemAccess, default));

        AssertDataHeaders(handler.Request!, "H");
        Assert.Equal("A9002-ZD-01", b.HspRef);
        Assert.Equal(128994.80m, b.BkyTtr);
        Assert.Equal(0m, b.BlkTtr);
        Assert.Equal("TRY", b.ParaBirimi);
        Assert.Null(b.KulKrdTtr);
    }

    [Fact]
    public async Task Balance_with_overdraft_and_negative_amount()
    {
        // mock-hhs store.map_balance biçimi: KMH'li hesapta krdHsp eklenir, bkyTtr negatif olabilir.
        var handler = new StubHandler(HttpStatusCode.OK, """
            [{"hspRef":"A1","bky":{"bkyTtr":"-1250.75","blkTtr":"0.00","prBrm":"TRY","bkyZmn":"2026-10-18T09:00:00+03:00"},
              "krdHsp":{"kulKrdTtr":"1250.75","krdDhlGstr":"0"}}]
            """);
        var sut = new MockHhsClient(new HttpClient(handler), Options.Create(new HhsOptions
        {
            Banks = [new HhsBankOptions { HhsKod = "9002", ApiBaseUrl = "http://hhs.test" }],
        }));

        var b = Assert.Single(await sut.GetBalancesAsync(UserAccess, default));

        Assert.Equal(-1250.75m, b.BkyTtr);
        Assert.Equal(1250.75m, b.KulKrdTtr);
        Assert.Equal("0", b.KrdDhlGstr);
    }

    [Fact]
    public async Task Transactions_send_window_and_paging_and_read_totals()
    {
        var (sut, handler) = Client("islemler", headers: new() { ["X-Total-Count"] = "9", ["X-Total-Pages"] = "1" });
        var window = new HhsWindow(new DateTimeOffset(2026, 9, 18, 0, 0, 0, Tr), new DateTimeOffset(2026, 10, 18, 12, 30, 0, Tr));

        var page = await sut.GetTransactionsAsync(UserAccess, "A9002-ZD-01", window, page: 1, pageSize: 100, default);

        var uri = handler.Request!.RequestUri!;
        Assert.Equal("/hesaplar/A9002-ZD-01/islemler", uri.AbsolutePath);
        var q = QueryHelpers.ParseQuery(uri.Query);
        Assert.Equal("2026-09-18T00:00:00+03:00", q["hesapIslemBslTrh"]);
        Assert.Equal("2026-10-18T12:30:00+03:00", q["hesapIslemBtsTrh"]);
        Assert.Equal("1", q["syfNo"]);
        Assert.Equal("100", q["syfKytSayi"]);

        Assert.Equal(9, page.TotalCount);
        Assert.Equal(1, page.TotalPages);
        Assert.Equal(9, page.Items.Count);
        var t = page.Items[0];
        Assert.Equal("20261016DEMO00001", t.IslNo);
        Assert.Equal(35000.00m, t.Tutar);
        Assert.Equal("A", t.BrcAlc);
        Assert.Equal("EFT", t.IslTur);
        Assert.Equal("06", t.IslAmc);
        Assert.Equal("EFT - ATLAS REKLAM AJANSI LTD STI - fatura", t.Aciklama);
        Assert.Equal("DORUK YAZILIM TEKNOLOJI AS", t.KarsiUnvan);
        Assert.Equal(new DateTimeOffset(2026, 10, 16, 17, 28, 9, Tr), t.Zaman);
        Assert.All(page.Items, i => Assert.True(i.Tutar > 0));
        Assert.All(page.Items, i => Assert.Contains(i.BrcAlc, new[] { "A", "B" }));
    }

    [Fact]
    public async Task Cards()
    {
        var (sut, _) = Client("kartlar");

        var k = Assert.Single(await sut.GetCardsAsync(UserAccess, default));

        Assert.Equal("K9002-ZD-01", k.KartRef);
        Assert.Equal("3307", k.SonNo);
        Assert.Equal("K", k.KartTipi);
        Assert.Equal("A", k.AltKartTipi);
        Assert.Equal("M", k.KartSema);
        Assert.Equal("Gold Kart", k.UrunAdi);
    }

    [Fact]
    public async Task Card_detail_dates_are_turkish_calendar_days()
    {
        var (sut, handler) = Client("kart-detay");

        var d = await sut.GetCardDetailAsync(UserAccess, "K9002-ZD-01", default);

        Assert.Equal("/kartlar/K9002-ZD-01/kart-detay", handler.Request!.RequestUri!.AbsolutePath);
        Assert.Equal(new DateOnly(2026, 10, 10), d.HesapKesimTarihi);
        Assert.Equal(new DateOnly(2026, 10, 20), d.SonOdemeTarihi);
        Assert.Equal(27182.54m, d.DonemBorcu);
        Assert.Equal(5436.51m, d.AsgariOdeme);
        Assert.Equal(60000.00m, d.Limit);
        Assert.Equal(28754.11m, d.KullanilanLimit);
    }

    [Fact]
    public async Task Card_transactions_include_installments_and_fx()
    {
        var (sut, _) = Client("kart-hareketleri", headers: new() { ["X-Total-Count"] = "31", ["X-Total-Pages"] = "1" });
        var window = new HhsWindow(DateTimeOffset.Now.AddDays(-30), DateTimeOffset.Now);

        var page = await sut.GetCardTransactionsAsync(UserAccess, "K9002-ZD-01", window, 1, 100, default);

        Assert.Equal(31, page.TotalCount);
        Assert.Equal(31, page.Items.Count);
        Assert.Contains(page.Items, m => m.TaksitSayisi > 1);

        var fx = page.Items.Single(m => m.IslemNo == "20260219330700116");
        Assert.Equal(867.93m, fx.Tutar);
        Assert.Equal("B", fx.BrcAlc);
        Assert.Equal("OPENAI *CHATGPT SUBSCR", fx.UyeIsyeri);
        Assert.Equal(new HhsFx(20.00m, "USD", 43.3967m), fx.Doviz);
        Assert.Null(fx.TaksitSayisi);
    }

    [Fact]
    public async Task Window_violation_maps_to_hhs_exception()
    {
        var (sut, _) = Client("hata-pencere", HttpStatusCode.BadRequest);
        var window = new HhsWindow(DateTimeOffset.Now.AddDays(-40), DateTimeOffset.Now);

        var ex = await Assert.ThrowsAsync<HhsException>(() => sut.GetTransactionsAsync(UserAccess, "A9002-ZD-01", window, 1, 50, default));

        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("TR.OHVPS.Business.InvalidStartEndTime", ex.ErrorCode);
    }

    [Fact]
    public async Task Quota_exceeded_is_recognisable()
    {
        var handler = new StubHandler((HttpStatusCode)429,
            """{"httpCode":429,"errorCode":"TR.OHVPS.Connection.ExceededRate","moreInformationTr":"Sistem tetikli sorgu kotası aşıldı."}""");
        var sut = new MockHhsClient(new HttpClient(handler), Options.Create(new HhsOptions
        {
            Banks = [new HhsBankOptions { HhsKod = "9002", ApiBaseUrl = "http://hhs.test" }],
        }));

        var ex = await Assert.ThrowsAsync<HhsException>(() => sut.GetBalancesAsync(SystemAccess, default));

        Assert.True(ex.IsRateExceeded);
    }
}
