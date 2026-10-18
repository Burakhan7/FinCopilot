using FinCopilot.Application.Abstractions;
using FinCopilot.Application.Auth;
using FinCopilot.Application.Consents;
using FinCopilot.Infrastructure.Auth;
using FinCopilot.Infrastructure.Hhs;
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

        return services;
    }
}
