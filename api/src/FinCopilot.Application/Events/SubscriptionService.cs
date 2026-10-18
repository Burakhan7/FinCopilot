using FinCopilot.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinCopilot.Application.Events;

public static class OlayTipi
{
    public const string KaynakGuncellendi = "KAYNAK_GUNCELLENDI";
    public const string AyrikGkdBasarili = "AYRIK_GKD_BASARILI";
    public const string AyrikGkdBasarisiz = "AYRIK_GKD_BASARISIZ";
}

public static class KaynakTipi
{
    public const string HesapBilgisiRizasi = "HESAP_BILGISI_RIZASI";
    public const string Bakiye = "BAKIYE";
}

/// <summary>Her bankada tek olay aboneliği; içinde ihtiyaç duyduğumuz tüm olay tipleri olmalı.</summary>
public sealed class SubscriptionService(IAppDbContext db, IHhsClient hhs, ILogger<SubscriptionService> logger)
{
    public static readonly IReadOnlyList<HhsEventType> RequiredTypes =
    [
        new(OlayTipi.KaynakGuncellendi, KaynakTipi.HesapBilgisiRizasi),
        new(OlayTipi.KaynakGuncellendi, KaynakTipi.Bakiye),
        new(OlayTipi.AyrikGkdBasarili, KaynakTipi.HesapBilgisiRizasi),
        new(OlayTipi.AyrikGkdBasarisiz, KaynakTipi.HesapBilgisiRizasi),
    ];

    /// <summary>Yoksa oluşturur, eksik tip varsa günceller. Ayrık GKD rızasından önce şarttır.</summary>
    public async Task EnsureAsync(string hhsKod, CancellationToken ct)
    {
        var current = await hhs.GetSubscriptionAsync(hhsKod, ct);
        if (current is null)
        {
            await hhs.CreateSubscriptionAsync(hhsKod, RequiredTypes, ct);
            logger.LogInformation("Olay aboneliği oluşturuldu: banka {HhsKod}", hhsKod);
        }
        else if (RequiredTypes.Except(current.Types).Any())
        {
            await hhs.UpdateSubscriptionAsync(hhsKod, current.OlayAbonelikNo, current.Types.Union(RequiredTypes).ToList(), ct);
            logger.LogInformation("Olay aboneliği güncellendi: banka {HhsKod}", hhsKod);
        }
    }

    /// <summary>Tüm aktif bankalar; bir banka hata verirse diğerleri yine denenir.</summary>
    public async Task<int> EnsureAllAsync(CancellationToken ct)
    {
        var failures = 0;
        foreach (var hhsKod in await db.Banks.Where(b => b.IsActive).Select(b => b.HhsKod).ToListAsync(ct))
        {
            try
            {
                await EnsureAsync(hhsKod, ct);
            }
            catch (Exception e) when (e is HhsException or HttpRequestException or TimeoutException)
            {
                failures++;
                logger.LogWarning(e, "Olay aboneliği kurulamadı: banka {HhsKod} (yarın tekrar denenecek)", hhsKod);
            }
        }
        return failures;
    }
}
