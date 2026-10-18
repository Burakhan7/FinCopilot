using FinCopilot.Application.Abstractions;
using FinCopilot.Application.Consents;
using FinCopilot.Domain.BankData;
using FinCopilot.Domain.Consents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinCopilot.Application.Sync;

/// <summary>
/// Bankadan veri çekme. İki tür çalıştırma:
/// <list type="bullet">
/// <item>İlk yükleme (backfill): bağlanınca, PSU-Initiated=E, rızanın izin verdiği geçmiş (≤12 ay), 30 günlük pencereler.</item>
/// <item>Zamanlı (scheduled): günde 4 kez, PSU-Initiated=H, son 24 saat; rıza başına günde en fazla 4 (TR günü).</item>
/// </list>
/// Hata sınıflandırması: kota/iptal/4xx → kaydedilir, tekrar denenmez; ağ ve 5xx → kaydedilir ve fırlatılır (Hangfire yeniden dener).
/// </summary>
public sealed class SyncService(
    IAppDbContext db,
    IHhsClient hhs,
    ConsentService consents,
    BankDataWriter writer,
    TimeProvider clock,
    ILogger<SyncService> logger)
{
    public const int DailySystemQuota = 4;
    public const int PageSize = 100;
    public static readonly TimeSpan PsuWindow = TimeSpan.FromDays(30);
    public static readonly TimeSpan SystemWindow = TimeSpan.FromHours(24);

    public Task<string> BackfillAsync(Guid consentId, CancellationToken ct) => RunAsync(consentId, SyncKind.Backfill, ct);

    public Task<string> ScheduledSyncAsync(Guid consentId, CancellationToken ct) => RunAsync(consentId, SyncKind.Scheduled, ct);

    /// <summary>Zamanlı senkrona girecek rızalar: bağlı (K) ve token'ı olanlar.</summary>
    public async Task<IReadOnlyList<Guid>> ActiveConsentIdsAsync(CancellationToken ct) =>
        await db.Consents.Where(c => c.RizaDrm == RizaDurumu.YetkiKullanildi && c.Token != null).Select(c => c.Id).ToListAsync(ct);

    /// <summary>Bugün (TR) bu rıza için HHS'ye giden sistem tetikli çalıştırma sayısı.</summary>
    public async Task<int> SystemRunsTodayAsync(Guid consentId, CancellationToken ct)
    {
        var dayStart = TurkeyTime.StartOfDay(clock.GetUtcNow());
        return await db.SyncJobs.CountAsync(j =>
            j.ConsentId == consentId && j.PsuInitiated == "H" && j.StartedAt >= dayStart
            // Yerelde kota yüzünden hiç denenmeyenler (HTTP kodu yok) sayılmaz.
            && !(j.Status == SyncStatus.Quota && j.HttpStatus == null), ct);
    }

    private async Task<string> RunAsync(Guid consentId, string kind, CancellationToken ct)
    {
        var consent = await db.Consents.Include(c => c.Token).FirstOrDefaultAsync(c => c.Id == consentId, ct);
        if (consent is null || consent.RizaDrm != RizaDurumu.YetkiKullanildi || consent.Token is null)
        {
            logger.LogInformation("Senkron atlandı: rıza {ConsentId} bağlı değil", consentId);
            return SyncStatus.Revoked;
        }

        var psu = kind == SyncKind.Backfill;
        var now = clock.GetUtcNow();
        var (start, end) = Window(consent, psu, now);
        var job = new SyncJob
        {
            Id = Guid.NewGuid(),
            ConsentId = consent.Id,
            Resource = kind,
            PsuInitiated = psu ? "E" : "H",
            WindowStart = start,
            WindowEnd = end,
            Status = SyncStatus.Running,
            StartedAt = now,
        };

        if (!psu && await SystemRunsTodayAsync(consent.Id, ct) >= DailySystemQuota)
        {
            job.Status = SyncStatus.Quota;
            job.FinishedAt = now;
            db.SyncJobs.Add(job);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Senkron atlandı: rıza {ConsentId} için günlük sistem sorgu kotası dolu", consent.Id);
            return job.Status;
        }

        db.SyncJobs.Add(job);
        await db.SaveChangesAsync(ct);

        try
        {
            var session = new Session(consent, psu, await consents.GetAccessTokenAsync(consent, ct));
            await FetchAsync(session, start, end, ct);
            job.Status = SyncStatus.Succeeded;
        }
        catch (HhsException e) when (e.IsRateExceeded)
        {
            Fail(job, SyncStatus.Quota, e);
        }
        catch (HhsException e) when (e.IsConsentRevoked)
        {
            Fail(job, SyncStatus.Revoked, e);
            await consents.SyncStatusAsync(consent, ct);
        }
        catch (HhsException e) when (e.StatusCode is >= 400 and < 500)
        {
            // Kalıcı istemci hatası: tekrar denemek aynı sonucu verir.
            Fail(job, SyncStatus.Failed, e);
            logger.LogWarning("Senkron başarısız (tekrar denenmeyecek): rıza {ConsentId}, {Status} {ErrorCode}: {Message}",
                consent.Id, e.StatusCode, e.ErrorCode, e.Message);
        }
        catch (Exception e) when (e is HhsException or HttpRequestException or TimeoutException)
        {
            Fail(job, SyncStatus.Failed, e as HhsException);
            job.ErrorCode ??= e.GetType().Name;
            job.FinishedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        job.FinishedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return job.Status;
    }

    private static (DateTimeOffset Start, DateTimeOffset End) Window(Consent consent, bool psu, DateTimeOffset now)
    {
        var end = consent.HesapIslBtsZmn is { } bts && bts < now ? bts : now;
        // Rıza aralığının başı dahil; saniye altı yuvarlama farklarına karşı 1 dk içeriden başla.
        var consentStart = (consent.HesapIslBslZmn ?? now.AddDays(-365)).AddMinutes(1);
        var start = psu ? consentStart : now - SystemWindow;
        return (start < consentStart ? consentStart : start, end);
    }

    private static void Fail(SyncJob job, string status, HhsException? e)
    {
        job.Status = status;
        job.HttpStatus = e?.StatusCode;
        job.ErrorCode = e?.ErrorCode;
    }

    private async Task FetchAsync(Session s, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var izin = s.Consent.IzinTurleri.ToHashSet();
        var window = s.Psu ? PsuWindow : SystemWindow;
        int txCount = 0, cardTxCount = 0;

        IReadOnlyList<Account> accounts = [];
        if (izin.Contains("01"))
            accounts = await writer.UpsertAccountsAsync(s.Consent, await CallAsync(s, a => hhs.GetAccountsAsync(a, ct), ct), ct);

        if (izin.Contains("03") && accounts.Count > 0)
            await writer.InsertBalancesAsync(accounts, await CallAsync(s, a => hhs.GetBalancesAsync(a, ct), ct), "sync", ct);

        if (izin.Contains("04"))
        {
            foreach (var account in accounts)
            foreach (var (ws, we) in SyncWindows.Split(start, end, window))
                txCount += await AllPagesAsync(s,
                    (a, page) => hhs.GetTransactionsAsync(a, account.HspRef, new HhsWindow(ws, we), page, PageSize, ct),
                    items => writer.InsertTransactionsAsync(account, items, ct), ct);
        }

        if (izin.Contains("07"))
        {
            var cards = await writer.UpsertCardsAsync(s.Consent, await CallAsync(s, a => hhs.GetCardsAsync(a, ct), ct), ct);

            // Banka kartı harcamaları hesap hareketlerinden okunur; detay ve hareket sadece kredi kartları için.
            foreach (var card in cards.Where(c => c.KartTipi == "K"))
            {
                if (izin.Contains("08"))
                    await writer.UpsertStatementAsync(card, await CallAsync(s, a => hhs.GetCardDetailAsync(a, card.KartRef, ct), ct), ct);

                if (izin.Contains("09"))
                {
                    foreach (var (ws, we) in SyncWindows.Split(start, end, window))
                        cardTxCount += await AllPagesAsync(s,
                            (a, page) => hhs.GetCardTransactionsAsync(a, card.KartRef, new HhsWindow(ws, we), page, PageSize, ct),
                            items => writer.InsertCardTransactionsAsync(card, items, ct), ct);
                }
            }
        }

        logger.LogInformation("Senkron tamam: rıza {ConsentId} ({Psu}), {Accounts} hesap, {Tx} yeni işlem, {CardTx} yeni kart hareketi",
            s.Consent.Id, s.Psu ? "E" : "H", accounts.Count, txCount, cardTxCount);
    }

    private async Task<int> AllPagesAsync<T>(
        Session s, Func<HhsAccess, int, Task<HhsPage<T>>> fetch, Func<IReadOnlyList<T>, Task<int>> write, CancellationToken ct)
    {
        int added = 0, page = 1, pages;
        do
        {
            var current = page;
            var result = await CallAsync(s, a => fetch(a, current), ct);
            added += await write(result.Items);
            pages = result.TotalPages;
        } while (++page <= pages);
        return added;
    }

    /// <summary>HHS 401 dönerse (belirteç süresi beklenenden önce bitmiş) bir kez yenileyip tekrar dener.</summary>
    private async Task<T> CallAsync<T>(Session s, Func<HhsAccess, Task<T>> call, CancellationToken ct)
    {
        try
        {
            return await call(s.Access);
        }
        catch (HhsException e) when (e.StatusCode == 401 && !s.Refreshed)
        {
            s.Refreshed = true;
            s.AccessToken = await consents.RefreshAccessTokenAsync(s.Consent, ct);
            await db.SaveChangesAsync(ct);
            return await call(s.Access);
        }
    }

    private sealed class Session(Consent consent, bool psu, string accessToken)
    {
        public Consent Consent { get; } = consent;
        public bool Psu { get; } = psu;
        public string AccessToken { get; set; } = accessToken;
        public bool Refreshed { get; set; }
        public HhsAccess Access => new(Consent.HhsKod, AccessToken, Psu);
    }
}
