using FinCopilot.Application.Consents;
using FinCopilot.Domain.Consents;
using FinCopilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FinCopilot.Infrastructure.Hhs;

/// <summary>
/// Hhs:Banks yapılandırmasını banks tablosuna yazar. Listede olmayan bankalar silinmez, pasifleşir
/// (eski rızalar FK ile bağlı). Gerçek ortamda kaynak HHS-YÖS API olacak.
/// </summary>
public static class BankCatalog
{
    public static async Task SyncAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var configured = scope.ServiceProvider.GetRequiredService<IOptions<HhsOptions>>().Value.Banks;

        var existing = await db.Banks.ToDictionaryAsync(b => b.HhsKod, ct);
        foreach (var o in configured)
        {
            if (!existing.TryGetValue(o.HhsKod, out var bank))
            {
                bank = new Bank { HhsKod = o.HhsKod, Name = o.Name, ApiBaseUrl = o.ApiBaseUrl };
                db.Banks.Add(bank);
            }
            bank.Name = o.Name;
            bank.ApiBaseUrl = o.ApiBaseUrl;
            bank.LogoUrl = o.LogoUrl;
            bank.SupportsDecoupled = o.SupportsDecoupled;
            bank.IsActive = true;
        }

        var configuredCodes = configured.Select(b => b.HhsKod).ToHashSet();
        foreach (var bank in existing.Values.Where(b => !configuredCodes.Contains(b.HhsKod)))
            bank.IsActive = false;

        await db.SaveChangesAsync(ct);
    }
}
