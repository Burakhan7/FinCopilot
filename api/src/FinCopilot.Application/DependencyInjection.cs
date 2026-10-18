using FinCopilot.Application.Auth;
using FinCopilot.Application.Behaviors;
using FinCopilot.Application.Consents;
using FinCopilot.Application.Events;
using FinCopilot.Application.Sync;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FinCopilot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<OtpService>();
        services.AddScoped<SessionIssuer>();
        services.AddScoped<ConsentService>();
        services.AddScoped<BankDataWriter>();
        services.AddScoped<SyncService>();
        services.AddScoped<SubscriptionService>();
        services.AddScoped<EventProcessor>();
        services.AddScoped<ConsentMaintenance>();

        return services;
    }
}
