namespace FinCopilot.Application.Abstractions;

/// <summary>Senkron işlerini arka plana (Hangfire → Worker) atar.</summary>
public interface ISyncJobScheduler
{
    void EnqueueBackfill(Guid consentId);
    void EnqueueScheduledSync(Guid consentId);
}
