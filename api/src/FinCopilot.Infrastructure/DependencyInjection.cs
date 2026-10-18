using FinCopilot.Application.Abstractions;
using FinCopilot.Application.Auth;
using FinCopilot.Infrastructure.Auth;
using FinCopilot.Infrastructure.Persistence;
using FinCopilot.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        return services;
    }
}
