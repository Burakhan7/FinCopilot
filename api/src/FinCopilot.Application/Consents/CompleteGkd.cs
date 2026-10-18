using FinCopilot.Application.Abstractions;
using FinCopilot.Application.Auth;
using FinCopilot.Application.Common;
using FinCopilot.Domain.Consents;
using FluentValidation;
using MediatR;

namespace FinCopilot.Application.Consents;

/// <summary>
/// Banka kullanıcıyı uygulamaya döndürdü (deep link). Mobil, gelen parametreleri aynen iletir.
/// Onay varsa yetKod gelir; kullanıcı vazgeçtiyse/iptal olduysa gelmez ve durum HHS'den okunur.
/// </summary>
public sealed record CompleteGkdCommand(
    Guid UserId,
    Guid ConsentId,
    string RizaNo,
    string DrmKod,
    string? YetKod) : IRequest<ConsentDto>;

public sealed class CompleteGkdValidator : AbstractValidator<CompleteGkdCommand>
{
    public CompleteGkdValidator()
    {
        RuleFor(x => x.RizaNo).NotEmpty().MaximumLength(64);
        RuleFor(x => x.DrmKod).NotEmpty().MaximumLength(128);
        RuleFor(x => x.YetKod).MaximumLength(256);
    }
}

public sealed class CompleteGkdHandler(IAppDbContext db, IHhsClient hhs, ConsentService consents, ISyncJobScheduler jobs)
    : IRequestHandler<CompleteGkdCommand, ConsentDto>
{
    public async Task<ConsentDto> Handle(CompleteGkdCommand request, CancellationToken ct)
    {
        var consent = await consents.GetOwnedAsync(request.UserId, request.ConsentId, ct);

        if (consent.RizaDrm != RizaDurumu.YetkiBekleniyor || consent.DrmKodHash is null)
            throw AppProblemException.Conflict("consent_not_pending", "Bu bağlantı isteği artık beklemede değil.");

        // CSRF: dönüş bizim başlattığımız akışa ait olmalı.
        if (consent.RizaNo != request.RizaNo
            || !SecretHash.FixedTimeEquals(consent.DrmKodHash, SecretHash.Sha256(request.DrmKod)))
            throw AppProblemException.BadRequest("drm_kod_invalid", "Banka dönüşü doğrulanamadı.");

        consent.DrmKodHash = null; // tek kullanımlık

        if (string.IsNullOrEmpty(request.YetKod))
        {
            await consents.SyncStatusAsync(consent, ct);
        }
        else
        {
            var token = await hhs.ExchangeAuthCodeAsync(consent.HhsKod, consent.RizaNo, request.YetKod, ct);
            consents.StoreTokens(consent, token);
            await consents.ApplyStatusAsync(consent, RizaDurumu.YetkiKullanildi, null, ct);
        }

        await db.SaveChangesAsync(ct);

        // 12 aylık ilk yükleme arka planda (Worker); mobil durumu GET /consents/{id} ile izler.
        if (consent.RizaDrm == RizaDurumu.YetkiKullanildi)
            jobs.EnqueueBackfill(consent.Id);

        return await consents.ToDtoAsync(consent, ct);
    }
}
