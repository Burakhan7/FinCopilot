using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinCopilot.Application.Abstractions;
using FinCopilot.Application.BankData;
using FinCopilot.Application.Consents;
using FinCopilot.Application.Sync;
using FinCopilot.Domain.Consents;
using FinCopilot.Infrastructure.Jobs;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinCopilot.IntegrationTests;

[Collection(ApiCollection.Name)]
public class SyncTests(ApiFactory factory)
{
    private const string Iban = "TR250900205202796328220556";

    private sealed record Connected(HttpClient Client, Guid ConsentId, string RizaNo, FakeHhsClient.State Bank);

    /// <summary>
    /// Örnek banka verisi: 2 hesap (biri KMH'de), 12 ay boyunca işlem (bir pencerede 130 işlem → sayfalama),
    /// bir kredi kartı (ekstre, taksit, döviz, iade) ve bir banka kartı.
    /// </summary>
    private static FakeHhsClient.BankData SampleData(DateTimeOffset now)
    {
        HhsTransaction Tx(string no, DateTimeOffset at, decimal amount, string brcAlc = "B", string? amc = null, string desc = "MARKET") =>
            new(no, "R" + no, amount, "TRY", at, "D", brcAlc, "UYE_ISYERI_ISLEMLERI", amc, desc, null, null, null);

        var a1 = new List<HhsTransaction>
        {
            Tx("T-OLD", now.AddDays(-300), 120m),
            Tx("T-SALARY", now.AddHours(-2), 35000m, "A", "05", "EFT - MAAS ODEMESI"),
        };
        a1.AddRange(Enumerable.Range(1, 130).Select(i => Tx($"T-BULK-{i:000}", now.AddDays(-80).AddHours(i), 10m + i)));

        return new FakeHhsClient.BankData
        {
            Accounts =
            [
                new HhsAccount("A1", Iban, "AYSE YILMAZ", "KADIKOY", "Vadesiz TL", "TRY", "B", "VADESIZ", "Vadesiz TL", "AKTIF", now.AddYears(-5)),
                new HhsAccount("A2", "TR330006100519786457841326", "AYSE YILMAZ", "KADIKOY", "KMH", "TRY", "B", "KREDILI_MEVDUAT_HESABI", "Artı Hesap", "AKTIF", null),
            ],
            Balances =
            [
                new HhsBalance("A1", 1000.50m, 0m, "TRY", now.AddMinutes(-5), null, null),
                new HhsBalance("A2", -250m, 0m, "TRY", now.AddMinutes(-5), 250m, "0"),
            ],
            Transactions = new() { ["A1"] = a1, ["A2"] = [Tx("T-A2", now.AddDays(-10), 99.90m)] },
            Cards =
            [
                new HhsCard("K1", "3307", "K", "A", "F", "B", "A", "M", "Gold Kart"),
                new HhsCard("D1", "1111", "B", "A", "F", "B", "A", "T", "Banka Kartı"),
            ],
            CardDetails = new()
            {
                ["K1"] = new HhsCardDetail("K1", new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 20), 27182.54m, 5436.51m, 60000m, 28754.11m, "TRY"),
            },
            CardTransactions = new()
            {
                ["K1"] =
                [
                    new HhsCardTransaction("C-TAKSIT", now.AddDays(-200), 12000m, "TRY", "B", "TAKSITLI_ALISVERIS", "VATAN BILGISAYAR", "VATAN BILGISAYAR", "R1", 6, null),
                    new HhsCardTransaction("C-FX", now.AddDays(-20), 867.93m, "TRY", "B", "ALISVERIS", "OPENAI *CHATGPT", "OPENAI *CHATGPT", "R2", null, new HhsFx(20m, "USD", 43.3967m)),
                    new HhsCardTransaction("C-IADE", now.AddDays(-5), 150m, "TRY", "A", "IADE", "IADE - ZARA", "ZARA", "R3", null, null),
                ],
                ["D1"] = [new HhsCardTransaction("D-1", now.AddDays(-1), 50m, "TRY", "B", "ALISVERIS", "BIM", "BIM", "R4", null, null)],
            },
        };
    }

    private async Task<Connected> ConnectAsync()
    {
        var client = await factory.RegisteredClientWithTcknAsync();
        var created = await (await client.PostAsJsonAsync("/consents", new { hhsKod = ApiFactory.TestBankCode })).Content.ReadFromJsonAsync<CreateConsentResult>();
        var uri = new Uri(created!.GkdUrl);
        var rizaNo = uri.AbsolutePath.Split('/').Last();
        var bank = factory.Hhs.Consents[rizaNo];
        bank.Data = SampleData(DateTimeOffset.UtcNow);

        var res = await client.PostAsJsonAsync($"/consents/{created.ConsentId}/callback", new
        {
            rizaNo, drmKod = QueryHelpers.ParseQuery(uri.Query)["drmKod"].ToString(), yetKod = factory.Hhs.Authorize(rizaNo),
        });
        res.EnsureSuccessStatusCode();
        return new Connected(client, created.ConsentId, rizaNo, bank);
    }

    private async Task<string> RunAsync(Func<SyncService, Task<string>> run)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await run(scope.ServiceProvider.GetRequiredService<SyncService>());
    }

    private Task<string> BackfillAsync(Guid consentId) => RunAsync(s => s.BackfillAsync(consentId, default));
    private Task<string> ScheduledAsync(Guid consentId) => RunAsync(s => s.ScheduledSyncAsync(consentId, default));

    private async Task<int> CountTransactionsAsync(Guid consentId)
    {
        await using var db = factory.CreateDbContext();
        return await db.Transactions.CountAsync(t => db.Accounts.Any(a => a.Id == t.AccountId && a.ConsentId == consentId));
    }

    [Fact]
    public async Task Connecting_a_bank_queues_the_initial_load()
    {
        var c = await ConnectAsync();

        Assert.Contains(c.ConsentId, factory.Jobs.Backfills);
    }

    [Fact]
    public async Task Backfill_loads_twelve_months_of_accounts_balances_transactions_and_cards()
    {
        var c = await ConnectAsync();

        Assert.Equal(SyncStatus.Succeeded, await BackfillAsync(c.ConsentId));

        await using var db = factory.CreateDbContext();
        var accounts = await db.Accounts.Where(a => a.ConsentId == c.ConsentId).OrderBy(a => a.HspRef).ToListAsync();
        Assert.Equal(["A1", "A2"], accounts.Select(a => a.HspRef));
        Assert.Equal("TR25 **** 0556", accounts[0].HspNoMasked);
        var protector = factory.Services.GetRequiredService<ISecretProtector>();
        Assert.Equal(Iban, protector.Unprotect(accounts[0].HspNoEnc!, SecretPurpose.AccountNumber));

        var kmh = await db.BalanceSnapshots.SingleAsync(b => b.AccountId == accounts[1].Id);
        Assert.Equal(-250m, kmh.BkyTtr);
        Assert.Equal(250m, kmh.KulKrdTtr);

        // 2 + 130 (sayfalı pencere) + 1
        Assert.Equal(133, await CountTransactionsAsync(c.ConsentId));
        var salary = await db.Transactions.SingleAsync(t => t.AccountId == accounts[0].Id && t.IslNo == "T-SALARY");
        Assert.Equal(35000m, salary.Amount);
        Assert.Equal("A", salary.Direction);
        Assert.Equal("05", salary.IslAmc);
        Assert.True(await db.Transactions.AnyAsync(t => t.AccountId == accounts[0].Id && t.IslNo == "T-OLD"));

        var cards = await db.Cards.Where(k => k.ConsentId == c.ConsentId).OrderBy(k => k.KartRef).ToListAsync();
        Assert.Equal(["D1", "K1"], cards.Select(k => k.KartRef));
        var credit = cards[1];
        var statement = await db.CardStatements.SingleAsync(s => s.CardId == credit.Id);
        Assert.Equal(new DateOnly(2026, 10, 20), statement.DueDate);
        Assert.Equal(5436.51m, statement.MinPayment);

        var cardTx = await db.CardTransactions.Where(m => m.CardId == credit.Id).ToDictionaryAsync(m => m.IslemNo);
        Assert.Equal(3, cardTx.Count);
        Assert.Equal(6, cardTx["C-TAKSIT"].InstallmentCount);
        Assert.Equal("A", cardTx["C-IADE"].Direction);
        using var fx = JsonDocument.Parse(cardTx["C-FX"].FxInfo!);
        Assert.Equal("USD", fx.RootElement.GetProperty("orijinalParaBirimi").GetString());

        // Banka kartı harcamaları hesap hareketlerinden okunur: kart hareketi çekilmez.
        Assert.False(await db.CardTransactions.AnyAsync(m => m.CardId == cards[0].Id));
        Assert.DoesNotContain(c.Bank.Calls, call => call.Path.Contains("D1"));

        // İlk yükleme kullanıcı tetikli (E) ve pencereler ≤ 1 ay
        Assert.All(c.Bank.Calls, call => Assert.True(call.Psu));
        Assert.All(c.Bank.Calls.Where(x => x.Window is not null), x => Assert.True(x.Window!.End - x.Window.Start <= TimeSpan.FromDays(30)));

        var job = await db.SyncJobs.SingleAsync(j => j.ConsentId == c.ConsentId);
        Assert.Equal((SyncKind.Backfill, "E", SyncStatus.Succeeded), (job.Resource, job.PsuInitiated, job.Status));
        Assert.NotNull(job.FinishedAt);
    }

    [Fact]
    public async Task Backfill_can_run_twice_without_duplicates()
    {
        var c = await ConnectAsync();
        await BackfillAsync(c.ConsentId);

        Assert.Equal(SyncStatus.Succeeded, await BackfillAsync(c.ConsentId));

        Assert.Equal(133, await CountTransactionsAsync(c.ConsentId));
        await using var db = factory.CreateDbContext();
        Assert.Equal(2, await db.Accounts.CountAsync(a => a.ConsentId == c.ConsentId));
        Assert.Equal(2, await db.BalanceSnapshots.CountAsync(b => db.Accounts.Any(a => a.Id == b.AccountId && a.ConsentId == c.ConsentId)));
    }

    [Fact]
    public async Task Scheduled_sync_picks_up_new_transactions_as_system_query()
    {
        var c = await ConnectAsync();
        await BackfillAsync(c.ConsentId);
        while (c.Bank.Calls.TryDequeue(out _)) { }
        c.Bank.Data.Transactions["A1"].Add(new HhsTransaction("T-NEW", "RN", 42m, "TRY", DateTimeOffset.UtcNow.AddMinutes(-30), "D", "B", "FAST", null, "FAST - KIRA", null, null, null));

        Assert.Equal(SyncStatus.Succeeded, await ScheduledAsync(c.ConsentId));

        Assert.Equal(134, await CountTransactionsAsync(c.ConsentId));
        Assert.All(c.Bank.Calls, call => Assert.False(call.Psu));
        Assert.All(c.Bank.Calls.Where(x => x.Window is not null), x => Assert.True(x.Window!.End - x.Window.Start <= TimeSpan.FromHours(24)));
    }

    [Fact]
    public async Task Fifth_system_sync_of_the_day_is_not_sent()
    {
        var c = await ConnectAsync();
        for (var i = 0; i < 4; i++)
            Assert.Equal(SyncStatus.Succeeded, await ScheduledAsync(c.ConsentId));
        var callsBefore = c.Bank.Calls.Count;

        Assert.Equal(SyncStatus.Quota, await ScheduledAsync(c.ConsentId));

        Assert.Equal(callsBefore, c.Bank.Calls.Count);
    }

    [Fact]
    public async Task Bank_rate_limit_is_recorded_without_retry()
    {
        var c = await ConnectAsync();
        c.Bank.NextErrors.Enqueue(new HhsException(429, "TR.OHVPS.Connection.ExceededRate", "Kota aşıldı."));

        Assert.Equal(SyncStatus.Quota, await ScheduledAsync(c.ConsentId));

        await using var db = factory.CreateDbContext();
        var job = await db.SyncJobs.SingleAsync(j => j.ConsentId == c.ConsentId);
        Assert.Equal(429, job.HttpStatus);
    }

    [Fact]
    public async Task Revocation_at_bank_stops_sync_and_deactivates_data()
    {
        var c = await ConnectAsync();
        await BackfillAsync(c.ConsentId);
        factory.Hhs.CancelAtBank(c.RizaNo, "02");

        Assert.Equal(SyncStatus.Revoked, await ScheduledAsync(c.ConsentId));

        await using var db = factory.CreateDbContext();
        var consent = await db.Consents.SingleAsync(x => x.Id == c.ConsentId);
        Assert.Equal(("I", "02"), (consent.RizaDrm, consent.RizaIptDtyKod));
        Assert.False(await db.Accounts.AnyAsync(a => a.ConsentId == c.ConsentId && a.IsActive));
        Assert.False(await db.ConsentTokens.AnyAsync(t => t.ConsentId == c.ConsentId));
        Assert.Equal(SyncStatus.Revoked, await ScheduledAsync(c.ConsentId)); // artık denenmez
    }

    [Fact]
    public async Task Expired_bank_token_is_refreshed_once()
    {
        var c = await ConnectAsync();
        factory.Hhs.InvalidateAccessToken(c.RizaNo);

        Assert.Equal(SyncStatus.Succeeded, await BackfillAsync(c.ConsentId));

        await using var db = factory.CreateDbContext();
        var token = await db.ConsentTokens.SingleAsync(t => t.ConsentId == c.ConsentId);
        var protector = factory.Services.GetRequiredService<ISecretProtector>();
        Assert.Equal(c.Bank.AccessToken, protector.Unprotect(token.AccessTokenEnc, SecretPurpose.BankToken));
    }

    [Fact]
    public async Task Transient_bank_error_fails_the_job_for_hangfire_retry()
    {
        var c = await ConnectAsync();
        c.Bank.NextErrors.Enqueue(new HhsException(503, null, "Servis geçici olarak kullanılamıyor."));

        await Assert.ThrowsAsync<HhsException>(() => BackfillAsync(c.ConsentId));

        await using var db = factory.CreateDbContext();
        var job = await db.SyncJobs.SingleAsync(j => j.ConsentId == c.ConsentId);
        Assert.Equal((SyncStatus.Failed, 503), (job.Status, job.HttpStatus));
        Assert.NotNull(job.FinishedAt);

        // Hangfire'ın tekrarı başarılı olur.
        Assert.Equal(SyncStatus.Succeeded, await BackfillAsync(c.ConsentId));
    }

    [Fact]
    public async Task Recurring_job_queues_only_connected_consents()
    {
        var connected = await ConnectAsync();
        var revoked = await ConnectAsync();
        await revoked.Client.DeleteAsync($"/consents/{revoked.ConsentId}");

        await using (var scope = factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<SyncJobs>().ScheduleAllAsync(default);

        Assert.Contains(connected.ConsentId, factory.Jobs.ScheduledSyncs);
        Assert.DoesNotContain(revoked.ConsentId, factory.Jobs.ScheduledSyncs);
    }

    [Fact]
    public async Task Accounts_and_cards_endpoints_show_synced_data()
    {
        var c = await ConnectAsync();
        await BackfillAsync(c.ConsentId);

        var accounts = await c.Client.GetFromJsonAsync<List<AccountDto>>("/accounts");
        var cards = await c.Client.GetFromJsonAsync<List<CardDto>>("/cards");
        var consent = await c.Client.GetFromJsonAsync<ConsentDto>($"/consents/{c.ConsentId}");

        var vadesiz = accounts!.Single(a => a.HspNoMasked == "TR25 **** 0556");
        Assert.Equal("Anadolu Bank", vadesiz.BankName);
        Assert.Equal(1000.50m, vadesiz.Balance!.BkyTtr);
        Assert.Equal(27182.54m, cards!.Single(k => k.KartTipi == "K").LastStatement!.PeriodDebt);
        Assert.Null(cards!.Single(k => k.KartTipi == "B").LastStatement);
        Assert.Equal(new LastSyncDto(SyncKind.Backfill, SyncStatus.Succeeded, consent!.LastSync!.StartedAt, consent.LastSync.FinishedAt), consent.LastSync);

        var stranger = await factory.RegisteredClientAsync();
        Assert.Empty((await stranger.GetFromJsonAsync<List<AccountDto>>("/accounts"))!);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/accounts")).StatusCode);
    }
}
