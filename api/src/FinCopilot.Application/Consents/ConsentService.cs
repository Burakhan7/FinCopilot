using FinCopilot.Application.Abstractions;
using FinCopilot.Application.Common;
using FinCopilot.Domain.Consents;
using Microsoft.EntityFrameworkCore;

namespace FinCopilot.Application.Consents;

public sealed record ConsentDto(
    Guid Id,
    string HhsKod,
    string BankName,
    string RizaDrm,
    string Durum,
    string? RizaIptDtyKod,
    string? IptalNedeni,
    IReadOnlyList<string> IzinTurleri,
    DateTimeOffset? ErisimIzniSonTrh,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    LastSyncDto? LastSync);

/// <summary>Son senkron çalıştırması; mobil "veriler yükleniyor" durumunu buradan gösterir.</summary>
/// <param name="Kind">backfill / scheduled</param>
/// <param name="Status">running / succeeded / failed / quota / revoked</param>
public sealed record LastSyncDto(string Kind, string Status, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt);

/// <summary>Rıza işlemlerinin ortak parçaları. Kaydetmek (SaveChanges) çağıranın işidir.</summary>
public sealed class ConsentService(IAppDbContext db, IHhsClient hhs, ISecretProtector protector, TimeProvider clock)
{
    /// <summary>Erişim belirtecinin bitmesine bu kadar kala yenilenir.</summary>
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(1);

    public async Task<Consent> GetOwnedAsync(Guid userId, Guid consentId, CancellationToken ct) =>
        await db.Consents.Include(c => c.Token).FirstOrDefaultAsync(c => c.Id == consentId && c.UserId == userId, ct)
        ?? throw new AppProblemException(404, "consent_not_found", "Banka bağlantısı bulunamadı.");

    /// <summary>Durumu HHS'den okur (bankadan iptal, süre aşımı gibi değişiklikler için).</summary>
    public async Task SyncStatusAsync(Consent consent, CancellationToken ct)
    {
        if (consent.RizaNo is null || !consent.IsActive)
            return;

        var remote = await hhs.GetConsentAsync(consent.HhsKod, consent.RizaNo, ct);
        consent.ErisimIzniSonTrh = remote.ErisimIzniSonTrh;
        await ApplyStatusAsync(consent, remote.RizaDrm, remote.RizaIptDtyKod, ct);
    }

    public async Task ApplyStatusAsync(Consent consent, string drm, string? iptalKodu, CancellationToken ct)
    {
        if (!consent.TransitionTo(drm, iptalKodu, clock.GetUtcNow()) || consent.IsActive)
            return;

        // Rıza bitti: veriler silinmez, pasifleşir (docs/veritabani.md §5). Token'lara artık gerek yok.
        await db.Accounts.Where(a => a.ConsentId == consent.Id && a.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsActive, false), ct);
        await db.Cards.Where(c => c.ConsentId == consent.Id && c.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsActive, false), ct);
        if (consent.Token is not null)
        {
            db.ConsentTokens.Remove(consent.Token);
            consent.Token = null;
        }
    }

    public void StoreTokens(Consent consent, HhsToken token)
    {
        var now = clock.GetUtcNow();
        consent.Token ??= new ConsentToken { ConsentId = consent.Id, AccessTokenEnc = [] };
        consent.Token.AccessTokenEnc = protector.Protect(token.AccessToken, SecretPurpose.BankToken);
        consent.Token.AccessExpiresAt = now + token.AccessLifetime;
        consent.Token.RefreshTokenEnc = protector.Protect(token.RefreshToken, SecretPurpose.BankToken);
        consent.Token.RefreshExpiresAt = now + token.RefreshLifetime;
        consent.Token.UpdatedAt = now;
    }

    /// <summary>Geçerli bir HHS erişim belirteci döndürür; süresi dolmak üzereyse yeniler.</summary>
    public async Task<string> GetAccessTokenAsync(Consent consent, CancellationToken ct)
    {
        var token = consent.Token
            ?? throw new InvalidOperationException($"Rıza {consent.Id} için erişim belirteci yok.");

        if (token.AccessExpiresAt - RefreshSkew > clock.GetUtcNow())
            return protector.Unprotect(token.AccessTokenEnc, SecretPurpose.BankToken);

        return await RefreshAccessTokenAsync(consent, ct);
    }

    /// <summary>Süresine bakmadan yeniler (HHS 401 döndürdüğünde).</summary>
    public async Task<string> RefreshAccessTokenAsync(Consent consent, CancellationToken ct)
    {
        var token = consent.Token
            ?? throw new InvalidOperationException($"Rıza {consent.Id} için erişim belirteci yok.");
        var refresh = protector.Unprotect(token.RefreshTokenEnc!, SecretPurpose.BankToken);
        var renewed = await hhs.RefreshAccessTokenAsync(consent.HhsKod, consent.RizaNo!, refresh, ct);
        StoreTokens(consent, renewed);
        return renewed.AccessToken;
    }

    public async Task<ConsentDto> ToDtoAsync(Consent c, CancellationToken ct)
    {
        var bankName = await db.Banks.Where(b => b.HhsKod == c.HhsKod).Select(b => b.Name).FirstAsync(ct);
        var lastSync = (await LastSyncsAsync(db, [c.Id], ct)).GetValueOrDefault(c.Id);
        return ToDto(c, bankName, lastSync);
    }

    public static ConsentDto ToDto(Consent c, string bankName, LastSyncDto? lastSync) => new(
        c.Id, c.HhsKod, bankName, c.RizaDrm, RizaDurumu.Aciklama(c.RizaDrm),
        c.RizaIptDtyKod, c.RizaIptDtyKod is null ? null : RizaIptalKodu.Aciklama(c.RizaIptDtyKod),
        c.IzinTurleri, c.ErisimIzniSonTrh, c.CreatedAt, c.UpdatedAt, lastSync);

    public static async Task<Dictionary<Guid, LastSyncDto>> LastSyncsAsync(IAppDbContext db, IReadOnlyCollection<Guid> consentIds, CancellationToken ct)
    {
        var jobs = await db.SyncJobs.AsNoTracking()
            .Where(j => consentIds.Contains(j.ConsentId))
            .GroupBy(j => j.ConsentId)
            .Select(g => g.OrderByDescending(j => j.StartedAt).First())
            .ToListAsync(ct);
        return jobs.ToDictionary(j => j.ConsentId, j => new LastSyncDto(j.Resource, j.Status, j.StartedAt, j.FinishedAt));
    }
}
