using FinCopilot.Application.Consents;
using FinCopilot.Application.Events;
using Hangfire;

namespace FinCopilot.Infrastructure.Jobs;

/// <summary>Olay işleme (POST /olay-dinleme sonrası).</summary>
[AutomaticRetry(Attempts = 3)]
public sealed class EventJobs(EventProcessor processor)
{
    public Task ProcessAsync(Guid inboxId, CancellationToken ct) => processor.ProcessAsync(inboxId, ct);
}

/// <summary>Gece bakımı: olay abonelikleri ve rıza süreleri.</summary>
[AutomaticRetry(Attempts = 3)]
public sealed class MaintenanceJobs(SubscriptionService subscriptions, ConsentMaintenance consents)
{
    public const string EnsureSubscriptionsId = "ensure-subscriptions";
    public const string EnsureSubscriptionsCron = "0 2 * * *";
    public const string ConsentMaintenanceId = "consent-maintenance";
    public const string ConsentMaintenanceCron = "0 3 * * *";

    public Task EnsureSubscriptionsAsync(CancellationToken ct) => subscriptions.EnsureAllAsync(ct);

    public Task ConsentMaintenanceAsync(CancellationToken ct) => consents.RunAsync(ct);
}
