using FinCopilot.Application.Abstractions;
using FinCopilot.Application.Common;
using FinCopilot.Domain.Consents;
using MediatR;

namespace FinCopilot.Application.Consents;

/// <summary>Kullanıcı bağlantıyı uygulamadan kaldırır (ÖHVPS iptal kodu 03).</summary>
public sealed record RevokeConsentCommand(Guid UserId, Guid ConsentId) : IRequest;

public sealed class RevokeConsentHandler(IAppDbContext db, IHhsClient hhs, ConsentService consents)
    : IRequestHandler<RevokeConsentCommand>
{
    public async Task Handle(RevokeConsentCommand request, CancellationToken ct)
    {
        var consent = await consents.GetOwnedAsync(request.UserId, request.ConsentId, ct);
        if (!consent.IsActive)
            throw AppProblemException.Conflict("consent_not_active", "Bu bağlantı zaten kapalı.");

        if (consent.RizaNo is not null)
        {
            try
            {
                var accessToken = consent.RizaDrm == RizaDurumu.YetkiKullanildi
                    ? await consents.GetAccessTokenAsync(consent, ct)
                    : null;
                await hhs.RevokeConsentAsync(consent.HhsKod, consent.RizaNo, accessToken, ct);
            }
            catch (HhsException e) when (e.IsConsentRevoked)
            {
                // Banka tarafında zaten bitmiş; gerçek durumu ve iptal kodunu HHS'den al.
                await consents.SyncStatusAsync(consent, ct);
                await db.SaveChangesAsync(ct);
                return;
            }
        }

        await consents.ApplyStatusAsync(consent, RizaDurumu.YetkiIptal, RizaIptalKodu.KullaniciYosUzerinden, ct);
        await db.SaveChangesAsync(ct);
    }
}
