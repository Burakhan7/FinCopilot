using FinCopilot.Application.Abstractions;
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

        return services;
    }
}
