using System.Collections.Concurrent;
using FinCopilot.Application.Abstractions;

namespace FinCopilot.IntegrationTests;

/// <summary>Testlerde Hangfire yerine: kuyruğa atılan işleri kaydeder, çalıştırmaz (testler servisleri doğrudan çağırır).</summary>
public sealed class RecordingScheduler : ISyncJobScheduler
{
    public ConcurrentQueue<Guid> Backfills { get; } = new();
    public ConcurrentQueue<Guid> ScheduledSyncs { get; } = new();
    public ConcurrentQueue<Guid> Events { get; } = new();

    public void EnqueueBackfill(Guid consentId) => Backfills.Enqueue(consentId);
    public void EnqueueScheduledSync(Guid consentId) => ScheduledSyncs.Enqueue(consentId);
    public void EnqueueEventProcessing(Guid inboxId) => Events.Enqueue(inboxId);
}
