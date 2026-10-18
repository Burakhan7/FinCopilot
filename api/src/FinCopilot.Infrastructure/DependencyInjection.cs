using FinCopilot.Application.Abstractions;
using FinCopilot.Application.Auth;
using FinCopilot.Application.Consents;
using FinCopilot.Infrastructure.Auth;
using FinCopilot.Infrastructure.Hhs;
using FinCopilot.Infrastructure.Jobs;
using Hangfire;
using FinCopilot.Infrastructure.Persistence;
using FinCopilot.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace FinCopilot.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "FinCopilot";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} tanımlı değil.");

        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddDataProtection()
            .SetApplicationName("FinCopilot")
            .PersistKeysToDbContext<AppDbContext>();
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();

        services.AddSingleton(TimeProvider.System);

        services.AddOptions<AuthOptions>().Bind(configuration.GetSection(AuthOptions.SectionName));
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IPinHasher, PinHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<ISmsSender, LoggingSmsSender>();

        services.AddOptions<SecurityOptions>().Bind(configuration.GetSection(SecurityOptions.SectionName));
        services.AddSingleton<ITcknHasher, HmacTcknHasher>();

        services.AddOptions<HhsOptions>().Bind(configuration.GetSection(HhsOptions.SectionName));
        services.AddHttpClient<IHhsClient, MockHhsClient>(MockHhsClient.HttpClientName)
            .AddStandardResilienceHandler(o =>
            {
                // Rıza oluşturma / token alma tekrar edilirse HHS'de mükerrer kayıt oluşur: sadece GET'ler yeniden denenir.
                o.Retry.DisableForUnsafeHttpMethods();
            });

        // Hangfire deposu (Api: sadece kuyruğa atar; Worker: AddHangfireServer ile yürütür).
        // İşler aynı veritabanında "HangFire" şemasında.
        services.AddHangfire(cfg => cfg
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString));
        services.AddScoped<SyncJobs>();
        services.AddScoped<EventJobs>();
        services.AddScoped<MaintenanceJobs>();
        services.AddSingleton<ISyncJobScheduler, HangfireSyncJobScheduler>();

        return services;
    }

    /// <summary>Tekrarlayan işleri kaydeder ve abonelikleri hemen kontrol ettirir (Worker açılışında).</summary>
    public static void RegisterRecurringJobs(IServiceProvider services)
    {
        var recurring = services.GetRequiredService<IRecurringJobManager>();
        // Hangfire saat dilimini ID ile saklayıp tekrar çözer: sistemde kayıtlı bir ID olmalı (IANA, Windows'ta da çalışır).
        var options = new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById(SyncJobs.TimeZoneId) };

        recurring.AddOrUpdate<SyncJobs>(SyncJobs.ScheduledSyncRecurringId,
            j => j.ScheduleAllAsync(CancellationToken.None), SyncJobs.ScheduledSyncCron, options);
        recurring.AddOrUpdate<MaintenanceJobs>(MaintenanceJobs.EnsureSubscriptionsId,
            j => j.EnsureSubscriptionsAsync(CancellationToken.None), MaintenanceJobs.EnsureSubscriptionsCron, options);
        recurring.AddOrUpdate<MaintenanceJobs>(MaintenanceJobs.ConsentMaintenanceId,
            j => j.ConsentMaintenanceAsync(CancellationToken.None), MaintenanceJobs.ConsentMaintenanceCron, options);

        services.GetRequiredService<IBackgroundJobClient>()
            .Enqueue<MaintenanceJobs>(j => j.EnsureSubscriptionsAsync(CancellationToken.None));
    }
}
