using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinCopilot.Application.Abstractions;
using FinCopilot.Application.Consents;
using FinCopilot.Application.Events;
using FinCopilot.Application.Sync;
using FinCopilot.Domain.Consents;
using FinCopilot.Domain.Outputs;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinCopilot.IntegrationTests;

[Collection(ApiCollection.Name)]
public class EventTests(ApiFactory factory)
{
    private sealed record Connected(HttpClient Client, Guid ConsentId, string RizaNo, FakeHhsClient.State Bank);

    /// <summary>Yönlendirmeli bağlantı + ilk yükleme.</summary>
    private async Task<Connected> ConnectAsync()
    {
        var client = await factory.RegisteredClientWithTcknAsync();
        var created = await (await client.PostAsJsonAsync("/consents", new { hhsKod = ApiFactory.TestBankCode })).Content.ReadFromJsonAsync<CreateConsentResult>();
        var uri = new Uri(created!.GkdUrl!);
        var rizaNo = uri.AbsolutePath.Split('/').Last();
        var bank = factory.Hhs.Consents[rizaNo];
        bank.Data = SyncTests.SampleData(DateTimeOffset.UtcNow);
        (await client.PostAsJsonAsync($"/consents/{created.ConsentId}/callback", new
        {
            rizaNo, drmKod = QueryHelpers.ParseQuery(uri.Query)["drmKod"].ToString(), yetKod = factory.Hhs.Authorize(rizaNo),
        })).EnsureSuccessStatusCode();
        await Scoped<SyncService>(s => s.BackfillAsync(created.ConsentId, default));
        return new Connected(client, created.ConsentId, rizaNo, bank);
    }

    /// <summary>Ayrık GKD talebi: kullanıcı FinCopilot'tan çıkmaz, bankanın uygulamasında onaylar.</summary>
    private async Task<(HttpClient Client, Guid ConsentId, string RizaNo)> StartDecoupledAsync(string hhsKod = "9002")
    {
        var client = await factory.RegisteredClientWithTcknAsync();
        var res = await client.PostAsJsonAsync("/consents", new { hhsKod, gkdYontem = "A" });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var created = (await res.Content.ReadFromJsonAsync<CreateConsentResult>())!;
        Assert.Equal("A", created.GkdYontem);
        Assert.Null(created.GkdUrl);
        await using var db = factory.CreateDbContext();
        var rizaNo = (await db.Consents.SingleAsync(c => c.Id == created.ConsentId)).RizaNo!;
        return (client, created.ConsentId, rizaNo);
    }

    private async Task<T> Scoped<TService, T>(Func<TService, Task<T>> run) where TService : notnull
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await run(scope.ServiceProvider.GetRequiredService<TService>());
    }

    private Task<string> Scoped<TService>(Func<TService, Task<string>> run) where TService : notnull => Scoped<TService, string>(run);

    private Task<HttpResponseMessage> PostEventAsync(
        string olayTipi, string kaynakTipi, string kaynakNo, string hhsKod = ApiFactory.TestBankCode, string yosKod = "8001", string? olayNo = null) =>
        factory.CreateClient().PostAsJsonAsync("/olay-dinleme", new
        {
            olayNo = olayNo ?? Guid.NewGuid().ToString(),
            olayAbonelikNo = "abonelik-1",
            olayTipi,
            kaynakTipi,
            kaynakNo,
            olayZamani = DateTimeOffset.UtcNow,
            katilimciBlg = new { hhsKod, yosKod },
        });

    /// <summary>Olayı gönderir ve Worker'ın yapacağı gibi işler.</summary>
    private async Task DeliverAsync(string olayTipi, string kaynakTipi, string kaynakNo, string hhsKod = ApiFactory.TestBankCode)
    {
        var olayNo = Guid.NewGuid().ToString();
        Assert.Equal(HttpStatusCode.Accepted, (await PostEventAsync(olayTipi, kaynakTipi, kaynakNo, hhsKod, olayNo: olayNo)).StatusCode);
        await using var db = factory.CreateDbContext();
        var id = (await db.EventInbox.SingleAsync(e => e.OlayNo == olayNo)).Id;
        await Scoped<EventProcessor, bool>(async p => { await p.ProcessAsync(id, default); return true; });
    }

    private async Task<Consent> ConsentAsync(Guid id)
    {
        await using var db = factory.CreateDbContext();
        return await db.Consents.SingleAsync(c => c.Id == id);
    }

    // ------------------------------------------------------------------ POST /olay-dinleme

    [Fact]
    public async Task Event_is_accepted_without_login_stored_once_and_queued()
    {
        var olayNo = Guid.NewGuid().ToString();

        var first = await PostEventAsync(OlayTipi.KaynakGuncellendi, KaynakTipi.Bakiye, "A1", olayNo: olayNo);
        var retry = await PostEventAsync(OlayTipi.KaynakGuncellendi, KaynakTipi.Bakiye, "A1", olayNo: olayNo);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, retry.StatusCode);
        await using var db = factory.CreateDbContext();
        var item = await db.EventInbox.SingleAsync(e => e.OlayNo == olayNo);
        Assert.Single(factory.Jobs.Events, id => id == item.Id);
        using var payload = JsonDocument.Parse(item.Payload);
        Assert.Equal("9001", payload.RootElement.GetProperty("katilimciBlg").GetProperty("hhsKod").GetString());
    }

    [Theory]
    [InlineData("9001", "9999")] // başka YÖS'ün olayı
    [InlineData("1234", "8001")] // tanımsız banka
    public async Task Foreign_or_unknown_events_are_rejected(string hhsKod, string yosKod)
    {
        var res = await PostEventAsync(OlayTipi.KaynakGuncellendi, KaynakTipi.Bakiye, "A1", hhsKod, yosKod);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task System_event_endpoint_accepts_and_stores()
    {
        var olayNo = Guid.NewGuid().ToString();

        var res = await factory.CreateClient().PostAsJsonAsync("/sistem-olay-dinleme", new { olayNo, olayTipi = "HHS_YOS_GUNCELLENDI" });

        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
        await using var db = factory.CreateDbContext();
        Assert.Equal("HHS_YOS_GUNCELLENDI", (await db.EventInbox.SingleAsync(e => e.OlayNo == olayNo)).OlayTipi);
    }

    // ------------------------------------------------------------------ olay işleme

    [Fact]
    public async Task Balance_event_refreshes_only_that_accounts_balance()
    {
        var c = await ConnectAsync();
        while (c.Bank.Calls.TryDequeue(out _)) { }
        c.Bank.Data.Balances[0] = c.Bank.Data.Balances[0] with { BkyTtr = 36000.50m, BkyZmn = DateTimeOffset.UtcNow };

        await DeliverAsync(OlayTipi.KaynakGuncellendi, KaynakTipi.Bakiye, "A1");

        await using var db = factory.CreateDbContext();
        var latest = await db.BalanceSnapshots
            .Where(b => db.Accounts.Any(a => a.Id == b.AccountId && a.ConsentId == c.ConsentId && a.HspRef == "A1"))
            .OrderByDescending(b => b.BkyZmn).FirstAsync();
        Assert.Equal((36000.50m, "event"), (latest.BkyTtr, latest.Source));
        var call = Assert.Single(c.Bank.Calls);
        Assert.Equal(("/hesaplar/A1/bakiye", false), (call.Path, call.Psu));
    }

    [Fact]
    public async Task Balance_events_have_their_own_daily_quota()
    {
        var c = await ConnectAsync();
        for (var i = 0; i < 4; i++)
            Assert.Equal(SyncStatus.Succeeded, await Scoped<SyncService>(s => s.ScheduledSyncAsync(c.ConsentId, default)));

        for (var i = 0; i < 4; i++)
            Assert.Equal(SyncStatus.Succeeded, await Scoped<SyncService>(s => s.EventBalanceAsync(c.ConsentId, "A1", default)));

        Assert.Equal(SyncStatus.Quota, await Scoped<SyncService>(s => s.EventBalanceAsync(c.ConsentId, "A1", default)));
    }

    [Fact]
    public async Task Consent_event_picks_up_revocation_at_bank()
    {
        var c = await ConnectAsync();
        factory.Hhs.CancelAtBank(c.RizaNo, "02");

        await DeliverAsync(OlayTipi.KaynakGuncellendi, KaynakTipi.HesapBilgisiRizasi, c.RizaNo);

        var consent = await ConsentAsync(c.ConsentId);
        Assert.Equal(("I", "02"), (consent.RizaDrm, consent.RizaIptDtyKod));
    }

    // ------------------------------------------------------------------ ayrık GKD

    [Fact]
    public async Task Decoupled_consent_ensures_subscription_and_completes_on_event()
    {
        var (_, consentId, rizaNo) = await StartDecoupledAsync();

        var request = factory.Hhs.Consents[rizaNo].Request;
        Assert.True(request.Decoupled);
        Assert.All(SubscriptionService.RequiredTypes, t => Assert.Contains(t, factory.Hhs.Subscriptions["9002"].Types));
        Assert.Equal("B", (await ConsentAsync(consentId)).RizaDrm);

        factory.Hhs.AuthorizeDecoupled(rizaNo);
        await DeliverAsync(OlayTipi.AyrikGkdBasarili, KaynakTipi.HesapBilgisiRizasi, rizaNo, "9002");

        Assert.Equal("K", (await ConsentAsync(consentId)).RizaDrm);
        await using var db = factory.CreateDbContext();
        Assert.True(await db.ConsentTokens.AnyAsync(t => t.ConsentId == consentId));
        Assert.Contains(consentId, factory.Jobs.Backfills);
    }

    [Fact]
    public async Task Decoupled_consent_completes_on_polling_when_event_is_lost()
    {
        var (client, consentId, rizaNo) = await StartDecoupledAsync();
        factory.Hhs.AuthorizeDecoupled(rizaNo);

        var dto = await client.GetFromJsonAsync<ConsentDto>($"/consents/{consentId}");

        Assert.Equal(("K", "A"), (dto!.RizaDrm, dto.GkdYontem));
        Assert.Contains(consentId, factory.Jobs.Backfills);

        // Olay sonradan gelirse zararsız: zaten bağlı.
        await DeliverAsync(OlayTipi.AyrikGkdBasarili, KaynakTipi.HesapBilgisiRizasi, rizaNo, "9002");
        Assert.Equal("K", (await ConsentAsync(consentId)).RizaDrm);
    }

    [Fact]
    public async Task Decoupled_consent_rejected_in_bank_app()
    {
        var (_, consentId, rizaNo) = await StartDecoupledAsync();
        factory.Hhs.CancelAtBank(rizaNo, "13");

        await DeliverAsync(OlayTipi.AyrikGkdBasarisiz, KaynakTipi.HesapBilgisiRizasi, rizaNo, "9002");

        var consent = await ConsentAsync(consentId);
        Assert.Equal(("I", "13"), (consent.RizaDrm, consent.RizaIptDtyKod));
    }

    [Fact]
    public async Task Decoupled_is_refused_for_banks_without_support()
    {
        var client = await factory.RegisteredClientWithTcknAsync();

        var res = await client.PostAsJsonAsync("/consents", new { hhsKod = "9003", gkdYontem = "A" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("decoupled_not_supported", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Redirect_callback_is_not_accepted_for_decoupled_consent()
    {
        var (client, consentId, rizaNo) = await StartDecoupledAsync();

        var res = await client.PostAsJsonAsync($"/consents/{consentId}/callback", new { rizaNo, drmKod = "x", yetKod = "y" });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    // ------------------------------------------------------------------ abonelik ve bakım işleri

    [Fact]
    public async Task Subscriptions_are_created_and_repaired_for_all_banks()
    {
        factory.Hhs.Subscriptions["9003"] = new HhsSubscription("eski-abonelik", [new HhsEventType(OlayTipi.KaynakGuncellendi, KaynakTipi.Bakiye)]);

        Assert.Equal(0, await Scoped<SubscriptionService, int>(s => s.EnsureAllAsync(default)));

        foreach (var hhsKod in new[] { "9001", "9002", "9003" })
            Assert.All(SubscriptionService.RequiredTypes, t => Assert.Contains(t, factory.Hhs.Subscriptions[hhsKod].Types));
        Assert.Equal("eski-abonelik", factory.Hhs.Subscriptions["9003"].OlayAbonelikNo); // güncellendi, yeniden yaratılmadı

        Assert.Equal(0, await Scoped<SubscriptionService, int>(s => s.EnsureAllAsync(default))); // ikinci çalıştırma zararsız
    }

    [Fact]
    public async Task Maintenance_closes_expired_consents()
    {
        var reportedByBank = await ConnectAsync();
        var notYetReported = await ConnectAsync();
        var past = DateTimeOffset.UtcNow.AddHours(-1);
        await using (var db = factory.CreateDbContext())
            await db.Consents.Where(c => c.Id == reportedByBank.ConsentId || c.Id == notYetReported.ConsentId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.ErisimIzniSonTrh, past));
        reportedByBank.Bank.Drm = "S";
        // Süre bankada da geçmiş ama banka durumu henüz S'ye çekmemiş (Mock HHS bunu okuma anında yapar).
        notYetReported.Bank.Request = notYetReported.Bank.Request with { ErisimIzniSonTrh = past };

        await Scoped<ConsentMaintenance, MaintenanceResult>(m => m.RunAsync(default));

        foreach (var c in new[] { reportedByBank, notYetReported })
        {
            Assert.Equal("S", (await ConsentAsync(c.ConsentId)).RizaDrm);
            await using var db = factory.CreateDbContext();
            Assert.False(await db.Accounts.AnyAsync(a => a.ConsentId == c.ConsentId && a.IsActive));
            Assert.False(await db.ConsentTokens.AnyAsync(t => t.ConsentId == c.ConsentId));
        }
    }

    [Fact]
    public async Task Maintenance_warns_once_before_expiry()
    {
        var c = await ConnectAsync();
        await using (var db = factory.CreateDbContext())
            await db.Consents.Where(x => x.Id == c.ConsentId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ErisimIzniSonTrh, DateTimeOffset.UtcNow.AddDays(5)));

        await Scoped<ConsentMaintenance, MaintenanceResult>(m => m.RunAsync(default));
        await Scoped<ConsentMaintenance, MaintenanceResult>(m => m.RunAsync(default));

        await using var check = factory.CreateDbContext();
        var consent = await check.Consents.SingleAsync(x => x.Id == c.ConsentId);
        var alert = await check.Alerts.SingleAsync(a => a.UserId == consent.UserId);
        Assert.Equal(AlertType.ConsentExpiring, alert.Type);
        Assert.Contains("Anadolu Bank", alert.Title);
        Assert.Equal("K", consent.RizaDrm);
    }

    [Fact]
    public async Task Maintenance_resolves_requests_stuck_waiting()
    {
        var client = await factory.RegisteredClientWithTcknAsync();
        var created = await (await client.PostAsJsonAsync("/consents", new { hhsKod = ApiFactory.TestBankCode })).Content.ReadFromJsonAsync<CreateConsentResult>();
        var rizaNo = new Uri(created!.GkdUrl!).AbsolutePath.Split('/').Last();
        factory.Hhs.CancelAtBank(rizaNo, "04"); // GKD süresi doldu
        await using (var db = factory.CreateDbContext())
            await db.Consents.Where(c => c.Id == created.ConsentId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.CreatedAt, DateTimeOffset.UtcNow.AddHours(-2)));

        await Scoped<ConsentMaintenance, MaintenanceResult>(m => m.RunAsync(default));

        var consent = await ConsentAsync(created.ConsentId);
        Assert.Equal(("I", "04"), (consent.RizaDrm, consent.RizaIptDtyKod));
    }
}
