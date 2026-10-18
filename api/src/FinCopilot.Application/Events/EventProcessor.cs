using System.Text.Json;
using FinCopilot.Application.Abstractions;
using FinCopilot.Application.Consents;
using FinCopilot.Application.Sync;
using FinCopilot.Domain.Consents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinCopilot.Application.Events;

/// <summary>
/// event_inbox kaydını işler (Worker). Olay sadece "bir şey değişti" sinyalidir; ne değiştiği HHS'den okunur.
/// Geçici hatalar fırlar (Hangfire yeniden dener, kayıt işlenmemiş kalır); diğer hatalar loglanıp kayıt kapatılır.
/// </summary>
public sealed class EventProcessor(
    IAppDbContext db,
    ConsentService consents,
    SyncService sync,
    ISyncJobScheduler jobs,
    TimeProvider clock,
    ILogger<EventProcessor> logger)
{
    public async Task ProcessAsync(Guid inboxId, CancellationToken ct)
    {
        var item = await db.EventInbox.FirstOrDefaultAsync(e => e.Id == inboxId, ct);
        if (item is null || item.ProcessedAt is not null)
            return;

        var hhsKod = HhsKodOf(item.Payload);
        try
        {
            switch (item.OlayTipi, item.KaynakTipi)
            {
                case (OlayTipi.KaynakGuncellendi, KaynakTipi.Bakiye):
                    await BalanceChangedAsync(hhsKod, item.KaynakNo!, ct);
                    break;

                case (OlayTipi.KaynakGuncellendi or OlayTipi.AyrikGkdBasarili or OlayTipi.AyrikGkdBasarisiz, KaynakTipi.HesapBilgisiRizasi):
                    await ConsentChangedAsync(hhsKod, item.KaynakNo!, ct);
                    break;

                default:
                    logger.LogInformation("Bilinmeyen olay yok sayıldı: {OlayTipi}/{KaynakTipi}", item.OlayTipi, item.KaynakTipi);
                    break;
            }
        }
        catch (HhsException e) when (e.StatusCode is >= 400 and < 500)
        {
            logger.LogWarning("Olay işlenemedi (tekrar denenmeyecek): {OlayNo} {OlayTipi}: {Status} {ErrorCode}",
                item.OlayNo, item.OlayTipi, e.StatusCode, e.ErrorCode);
        }

        item.ProcessedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Rıza durumu değişti / ayrık GKD sonuçlandı: durumu HHS'den oku; onaylandıysa token al ve ilk yüklemeyi başlat.</summary>
    private async Task ConsentChangedAsync(string? hhsKod, string rizaNo, CancellationToken ct)
    {
        var consent = await db.Consents.Include(c => c.Token)
            .FirstOrDefaultAsync(c => c.RizaNo == rizaNo && (hhsKod == null || c.HhsKod == hhsKod), ct);
        if (consent is null)
        {
            logger.LogInformation("Olaydaki rıza bizde yok: {RizaNo}", rizaNo);
            return;
        }

        var connected = await consents.SyncStatusAsync(consent, ct);
        await db.SaveChangesAsync(ct);
        if (connected)
            jobs.EnqueueBackfill(consent.Id);
    }

    /// <summary>Hesap bakiyesi değişti: kaynakNo = hspRef.</summary>
    private async Task BalanceChangedAsync(string? hhsKod, string hspRef, CancellationToken ct)
    {
        var consentIds = await db.Accounts
            .Where(a => a.HspRef == hspRef && a.IsActive && (hhsKod == null || a.HhsKod == hhsKod))
            .Join(db.Consents.Where(c => c.RizaDrm == RizaDurumu.YetkiKullanildi), a => a.ConsentId, c => c.Id, (a, c) => c.Id)
            .Distinct()
            .ToListAsync(ct);

        foreach (var consentId in consentIds)
            await sync.EventBalanceAsync(consentId, hspRef, ct);
    }

    private static string? HhsKodOf(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            return doc.RootElement.TryGetProperty("katilimciBlg", out var k) && k.TryGetProperty("hhsKod", out var h)
                ? h.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
