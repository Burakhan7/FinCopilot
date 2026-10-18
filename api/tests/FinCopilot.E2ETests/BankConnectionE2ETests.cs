using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinCopilot.Application.BankData;
using FinCopilot.Application.Consents;
using FinCopilot.Domain.Consents;
using FinCopilot.Infrastructure.Jobs;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinCopilot.E2ETests;

/// <summary>
/// Faz 2 kilometre taşı, gerçek bileşenlerle: "Banka bağla → bankada onay → hesaplar geldi".
/// Kullanıcılar Mock HHS personaları (docs/mock-veri-parametreleri.md).
/// </summary>
[Collection(E2ECollection.Name)]
public class BankConnectionE2ETests(E2EEnvironment env)
{
    private const string Mehmet = "10000000214";   // Anadolu 9001 + Marmara 9003 (KMH, taksitli kart, banka kartı)
    private const string Zeynep = "10000000382";   // Ege 9002
    private const string Ayse = "10000000146";     // Anadolu 9001

    private async Task<JsonElement> MockConsentAsync(string hhsKod, string rizaNo)
    {
        using var mock = env.CreateMockClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, $"hesap-bilgisi-rizasi/{rizaNo}");
        req.Headers.Add("X-Request-ID", Guid.NewGuid().ToString());
        req.Headers.Add("X-ASPSP-Code", hhsKod);
        req.Headers.Add("X-TPP-Code", E2EEnvironment.YosKod);
        var res = await mock.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rzBlg");
    }

    private async Task<T> DbAsync<T>(Func<Infrastructure.Persistence.AppDbContext, Task<T>> query)
    {
        await using var db = env.CreateDbContext();
        return await query(db);
    }

    private Task<string?> SyncStatusAsync(Guid consentId, string kind) => DbAsync(db => db.SyncJobs
        .Where(j => j.ConsentId == consentId && j.Resource == kind).OrderByDescending(j => j.StartedAt)
        .Select(j => j.Status).FirstOrDefaultAsync());

    private Task<Consent> LocalConsentAsync(Guid id) => DbAsync(db => db.Consents.AsNoTracking().SingleAsync(c => c.Id == id));

    [Fact]
    public async Task Redirect_gkd_loads_twelve_months_then_scheduled_sync_and_revoke_work()
    {
        var user = await AppUser.RegisterAsync(env, "Mehmet Kaya", Mehmet);
        var bank = new BankUser(env, Mehmet);

        // 1) Banka bağla → banka sayfası → onay → deep link ile dönüş
        var started = await user.StartConnectingAsync("9003");
        Assert.StartsWith(env.MockBase.ToString(), started.GkdUrl);
        var link = await bank.CompleteRedirectGkdAsync(started.GkdUrl!);
        Assert.Equal("fincopilot", link.Uri.Scheme);
        Assert.NotNull(link["yetKod"]);

        var connected = await user.ReturnFromBankAsync(started.ConsentId, link);
        Assert.Equal("K", connected.RizaDrm);

        // 2) İlk yükleme Hangfire'da (Worker işi) çalışır; mobil ilerlemeyi GET /consents/{id} ile izler.
        var done = await Eventually.GetAsync(() => user.ConsentAsync(started.ConsentId),
            c => c.LastSync is { Kind: SyncKind.Backfill, Status: SyncStatus.Succeeded or SyncStatus.Failed }, "ilk yükleme");
        Assert.Equal(SyncStatus.Succeeded, done.LastSync!.Status);

        // 3) Hesaplar geldi
        var accounts = (await user.Api.GetFromJsonAsync<List<AccountDto>>("/accounts"))!;
        var kmh = Assert.Single(accounts, a => a.HspTip == "KREDILI_MEVDUAT_HESABI");
        Assert.Equal("Marmara Bank", kmh.BankName);
        Assert.Matches(@"^TR\d\d \*\*\*\* \d{4}$", kmh.HspNoMasked!);
        Assert.All(accounts, a => Assert.NotNull(a.Balance));

        var cards = (await user.Api.GetFromJsonAsync<List<CardDto>>("/cards"))!;
        var credit = Assert.Single(cards, c => c.KartTipi == "K");
        Assert.NotNull(credit.LastStatement);
        Assert.Null(Assert.Single(cards, c => c.KartTipi == "B").LastStatement);

        await using (var db = env.CreateDbContext())
        {
            var cardIds = await db.Cards.Where(c => c.ConsentId == started.ConsentId).ToDictionaryAsync(c => c.KartTipi, c => c.Id);
            Assert.True(await db.CardTransactions.AnyAsync(m => m.CardId == cardIds["K"] && m.InstallmentCount > 1), "taksitli alışveriş gelmeli");
            Assert.False(await db.CardTransactions.AnyAsync(m => m.CardId == cardIds["B"]), "banka kartı hareketi hesaptan okunur");
            var oldest = await db.Transactions.Where(t => db.Accounts.Any(a => a.Id == t.AccountId && a.ConsentId == started.ConsentId)).MinAsync(t => t.OccurredAt);
            Assert.True(oldest < DateTimeOffset.UtcNow.AddDays(-300), $"12 aylık geçmiş gelmeli (en eski: {oldest:yyyy-MM-dd})");
        }

        // 4) Zamanlı senkron (Hangfire tekrarlayan işi) sistem tetikli sorgularla çalışır
        env.Services.GetRequiredService<IRecurringJobManager>().Trigger(SyncJobs.ScheduledSyncRecurringId);
        Assert.Equal(SyncStatus.Succeeded, await Eventually.GetAsync(() => SyncStatusAsync(started.ConsentId, SyncKind.Scheduled),
            s => s is SyncStatus.Succeeded or SyncStatus.Failed or SyncStatus.Quota, "zamanlı senkron"));

        // 5) Uygulamadan kaldır → bankada da iptal (03)
        Assert.Equal(HttpStatusCode.NoContent, (await user.Api.DeleteAsync($"/consents/{started.ConsentId}")).StatusCode);
        var remote = await MockConsentAsync("9003", link["rizaNo"]!);
        Assert.Equal(("I", "03"), (remote.GetProperty("rizaDrm").GetString(), remote.GetProperty("rizaIptDtyKod").GetString()));
        Assert.Empty((await user.Api.GetFromJsonAsync<List<AccountDto>>("/accounts"))!);
    }

    [Fact]
    public async Task Decoupled_gkd_and_bank_events_flow_without_polling()
    {
        var user = await AppUser.RegisterAsync(env, "Zeynep Demir", Zeynep);
        var bank = new BankUser(env, Zeynep);

        // 1) Ayrık GKD: kullanıcı uygulamadan çıkmaz, bankanın kendi uygulamasında onaylar.
        var started = await user.StartConnectingAsync("9002", "A");
        Assert.Null(started.GkdUrl);
        var rizaNo = (await LocalConsentAsync(started.ConsentId)).RizaNo!;
        await bank.DecideInBankAppAsync("9002", rizaNo);

        // 2) AYRIK_GKD_BASARILI olayı Api'ye gelir, Worker bağlantıyı kurar. GET çağrılmıyor: sadece olay yolu.
        await Eventually.GetAsync(() => LocalConsentAsync(started.ConsentId), c => c.RizaDrm == "K", "olayla bağlanma");
        Assert.True(await DbAsync(db => db.EventInbox.AnyAsync(e => e.KaynakNo == rizaNo && e.OlayTipi == "AYRIK_GKD_BASARILI" && e.ProcessedAt != null)));
        Assert.Equal(SyncStatus.Succeeded, await Eventually.GetAsync(() => SyncStatusAsync(started.ConsentId, SyncKind.Backfill),
            s => s is SyncStatus.Succeeded or SyncStatus.Failed, "ilk yükleme"));

        // 3) Maaş yattı → BAKIYE olayı → sadece o hesabın bakiyesi
        const string hspRef = "A9002-ZD-01";
        Task<decimal?> LatestBalance(string? source) => DbAsync(db => db.BalanceSnapshots
            .Where(b => db.Accounts.Any(a => a.Id == b.AccountId && a.ConsentId == started.ConsentId && a.HspRef == hspRef) && (source == null || b.Source == source))
            .OrderByDescending(b => b.BkyZmn).Select(b => (decimal?)b.BkyTtr).FirstOrDefaultAsync());
        var before = (await LatestBalance(null))!.Value;

        using (var mock = env.CreateMockClient())
        {
            (await mock.PostAsync("_mock/simulasyon/olay-tick", null)).EnsureSuccessStatusCode(); // tarama başlangıcı
            await Task.Delay(1500);
            (await mock.PostAsJsonAsync("_mock/simulasyon/islem", new
            {
                tckn = Zeynep, hspRef, tutar = "35000.00", islAmc = "05", aciklama = "EFT - DORUK YAZILIM - MAAS ODEMESI",
            })).EnsureSuccessStatusCode();
        }
        var after = await Eventually.GetAsync(() => LatestBalance("event"), b => b is not null, "BAKIYE olayı");
        Assert.Equal(before + 35000m, after);

        // 4) Kullanıcı rızayı bankanın kendi kanalından iptal etti → rıza olayı
        using (var mock = env.CreateMockClient())
            (await mock.PostAsync($"_mock/simulasyon/riza-iptal/{rizaNo}", null)).EnsureSuccessStatusCode();
        var revoked = await Eventually.GetAsync(() => LocalConsentAsync(started.ConsentId), c => c.RizaDrm == "I", "bankadan iptal olayı");
        Assert.Equal("02", revoked.RizaIptDtyKod);
        Assert.False(await DbAsync(db => db.Accounts.AnyAsync(a => a.ConsentId == started.ConsentId && a.IsActive)));
    }

    [Fact]
    public async Task User_gives_up_at_bank()
    {
        var user = await AppUser.RegisterAsync(env, "Ayşe Yılmaz", Ayse);
        var started = await user.StartConnectingAsync("9001");

        var link = await new BankUser(env, Ayse).CompleteRedirectGkdAsync(started.GkdUrl!, approve: false);

        Assert.Null(link["yetKod"]);
        var consent = await user.ReturnFromBankAsync(started.ConsentId, link);
        Assert.Equal(("I", "13", "Bankada vazgeçildi"), (consent.RizaDrm, consent.RizaIptDtyKod, consent.IptalNedeni));
        Assert.Null(consent.LastSync);
    }

    [Fact]
    public async Task Logging_into_bank_as_someone_else_is_rejected_by_bank()
    {
        // Uygulamadaki kimlik ile bankaya giren kişi farklı: banka rızayı 08 ile iptal eder.
        var user = await AppUser.RegisterAsync(env, "Başka Biri", RandomTckn());
        var started = await user.StartConnectingAsync("9001");

        var link = await new BankUser(env, Mehmet).CompleteRedirectGkdAsync(started.GkdUrl!);

        var consent = await user.ReturnFromBankAsync(started.ConsentId, link);
        Assert.Equal(("I", "08"), (consent.RizaDrm, consent.RizaIptDtyKod));
    }

    private static string RandomTckn()
    {
        var d = new int[11];
        d[0] = Random.Shared.Next(2, 10); // persona TCKN'leri 1 ile başlıyor
        for (var i = 1; i < 9; i++) d[i] = Random.Shared.Next(0, 10);
        d[9] = (((d[0] + d[2] + d[4] + d[6] + d[8]) * 7 - (d[1] + d[3] + d[5] + d[7])) % 10 + 10) % 10;
        d[10] = d[..10].Sum() % 10;
        return string.Concat(d);
    }
}
