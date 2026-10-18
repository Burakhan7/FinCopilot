using FinCopilot.Application.Abstractions;
using FinCopilot.Application.Sync;
using Hangfire;

namespace FinCopilot.Infrastructure.Jobs;

/// <summary>
/// Hangfire iş giriş noktaları (Worker yürütür). Mantık <see cref="SyncService"/>'te; burada sadece kuyruk/zamanlama.
/// Kota, iptal ve 4xx hataları SyncService içinde kaydedilip yutulur; sadece geçici hatalar fırlar ve yeniden denenir.
/// </summary>
[AutomaticRetry(Attempts = 3)]
public sealed class SyncJobs(SyncService sync, ISyncJobScheduler scheduler)
{
    public const string ScheduledSyncRecurringId = "scheduled-sync";

    /// <summary>Günde 4 kez (00, 06, 12, 18 TR).</summary>
    public const string ScheduledSyncCron = "0 0,6,12,18 * * *";

    public const string TimeZoneId = "Europe/Istanbul";

    [Queue("default")]
    public Task BackfillAsync(Guid consentId, CancellationToken ct) => sync.BackfillAsync(consentId, ct);

    public Task ScheduledSyncAsync(Guid consentId, CancellationToken ct) => sync.ScheduledSyncAsync(consentId, ct);

    /// <summary>Her bağlı rıza için ayrı iş: biri başarısız olursa diğerlerini etkilemez.</summary>
    public async Task ScheduleAllAsync(CancellationToken ct)
    {
        foreach (var id in await sync.ActiveConsentIdsAsync(ct))
            scheduler.EnqueueScheduledSync(id);
    }
}

internal sealed class HangfireSyncJobScheduler(IBackgroundJobClient jobs) : ISyncJobScheduler
{
    public void EnqueueBackfill(Guid consentId) =>
        jobs.Enqueue<SyncJobs>(j => j.BackfillAsync(consentId, CancellationToken.None));

    public void EnqueueScheduledSync(Guid consentId) =>
        jobs.Enqueue<SyncJobs>(j => j.ScheduledSyncAsync(consentId, CancellationToken.None));

    public void EnqueueEventProcessing(Guid inboxId) =>
        jobs.Enqueue<EventJobs>(j => j.ProcessAsync(inboxId, CancellationToken.None));
}
