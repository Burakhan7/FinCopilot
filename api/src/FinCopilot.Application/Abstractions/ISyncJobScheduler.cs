namespace FinCopilot.Application.Abstractions;

/// <summary>Senkron ve olay işlerini arka plana (Hangfire → Worker) atar.</summary>
public interface ISyncJobScheduler
{
    void EnqueueBackfill(Guid consentId);
    void EnqueueScheduledSync(Guid consentId);
    /// <summary>event_inbox kaydını işler (POST /olay-dinleme 202 döndükten sonra).</summary>
    void EnqueueEventProcessing(Guid inboxId);
}
