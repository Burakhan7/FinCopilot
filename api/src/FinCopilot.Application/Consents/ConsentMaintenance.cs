using System.Text.Json;
using FinCopilot.Application.Abstractions;
using FinCopilot.Domain.Consents;
using FinCopilot.Domain.Outputs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinCopilot.Application.Consents;

public sealed record MaintenanceResult(int Expired, int StaleChecked, int Warned);

/// <summary>
/// Gece çalışan rıza bakımı (check_consent_expiry):
/// <list type="number">
/// <item>Erişim süresi geçmiş bağlı rızalar → HHS'den tazelenir; HHS hâlâ K diyorsa yerelde S yapılır.</item>
/// <item>1 saattir B/Y'de kalan talepler → HHS'den tazelenir (süre aşımı; ayrık GKD olayı kaçtıysa tamamlanır).</item>
/// <item>7 gün içinde bitecek rızalar → tek seferlik uyarı (alerts, consent_expiring). Push Faz 6'da.</item>
/// </list>
/// </summary>
public sealed class ConsentMaintenance(
    IAppDbContext db,
    ConsentService consents,
    ISyncJobScheduler jobs,
    TimeProvider clock,
    ILogger<ConsentMaintenance> logger)
{
    public static readonly TimeSpan WarnBefore = TimeSpan.FromDays(7);
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(1);

    public async Task<MaintenanceResult> RunAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        var expired = await db.Consents.Include(c => c.Token)
            .Where(c => c.RizaDrm == RizaDurumu.YetkiKullanildi && c.ErisimIzniSonTrh <= now)
            .ToListAsync(ct);
        foreach (var c in expired)
        {
            await TrySyncAsync(c, ct);
            if (c.RizaDrm == RizaDurumu.YetkiKullanildi && c.ErisimIzniSonTrh <= now)
                await consents.ApplyStatusAsync(c, RizaDurumu.YetkiSonlandirildi, null, ct);
            await db.SaveChangesAsync(ct);
        }

        var staleBefore = now - StaleAfter;
        var stale = await db.Consents.Include(c => c.Token)
            .Where(c => (c.RizaDrm == RizaDurumu.YetkiBekleniyor || c.RizaDrm == RizaDurumu.Yetkilendirildi) && c.CreatedAt <= staleBefore)
            .ToListAsync(ct);
        foreach (var c in stale)
        {
            var connected = await TrySyncAsync(c, ct);
            await db.SaveChangesAsync(ct);
            if (connected)
                jobs.EnqueueBackfill(c.Id);
        }

        var warned = await WarnExpiringAsync(now, ct);

        logger.LogInformation("Rıza bakımı: {Expired} süresi dolan, {Stale} bekleyen kontrol edildi, {Warned} uyarı",
            expired.Count, stale.Count, warned);
        return new MaintenanceResult(expired.Count, stale.Count, warned);
    }

    private async Task<bool> TrySyncAsync(Consent c, CancellationToken ct)
    {
        try
        {
            return await consents.SyncStatusAsync(c, ct);
        }
        catch (HhsException e) when (e.StatusCode is >= 400 and < 500)
        {
            logger.LogWarning("Rıza {ConsentId} HHS'den tazelenemedi: {Status} {ErrorCode}", c.Id, e.StatusCode, e.ErrorCode);
            return false;
        }
    }

    private async Task<int> WarnExpiringAsync(DateTimeOffset now, CancellationToken ct)
    {
        var limit = now + WarnBefore;
        var expiring = await (
            from c in db.Consents
            join b in db.Banks on c.HhsKod equals b.HhsKod
            where c.RizaDrm == RizaDurumu.YetkiKullanildi && c.ErisimIzniSonTrh > now && c.ErisimIzniSonTrh <= limit
            select new { c.Id, c.UserId, c.HhsKod, b.Name, SonTrh = c.ErisimIzniSonTrh!.Value }).ToListAsync(ct);

        var added = 0;
        foreach (var e in expiring)
        {
            var key = $"{AlertType.ConsentExpiring}:{e.Id}";
            if (await db.Alerts.AnyAsync(a => a.UserId == e.UserId && a.DedupeKey == key, ct))
                continue;

            db.Alerts.Add(new Alert
            {
                UserId = e.UserId,
                Type = AlertType.ConsentExpiring,
                Severity = "warning",
                Title = $"{e.Name} bağlantınızın süresi {e.SonTrh.ToOffset(Sync.TurkeyTime.Offset):dd.MM.yyyy} tarihinde doluyor",
                Payload = JsonSerializer.Serialize(new { consentId = e.Id, hhsKod = e.HhsKod, erisimIzniSonTrh = e.SonTrh }),
                DedupeKey = key,
                CreatedAt = now,
            });
            added++;
        }
        await db.SaveChangesAsync(ct);
        return added;
    }
}
