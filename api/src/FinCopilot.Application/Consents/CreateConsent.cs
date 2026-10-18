using System.Buffers.Text;
using System.Security.Cryptography;
using FinCopilot.Application.Abstractions;
using FinCopilot.Application.Auth;
using FinCopilot.Application.Common;
using FinCopilot.Application.Events;
using FinCopilot.Domain.Consents;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinCopilot.Application.Consents;

/// <summary>
/// "Banka bağla": HHS'de rıza açar.
/// <list type="bullet">
/// <item>Y (yönlendirmeli): mobil <see cref="CreateConsentResult.GkdUrl"/>'yi açar, banka deep link ile döner → callback.</item>
/// <item>A (ayrık): kullanıcı bankanın kendi uygulamasında onaylar; sonuç olayla (veya GET /consents/{id} ile) gelir.</item>
/// </list>
/// </summary>
public sealed record CreateConsentCommand(Guid UserId, string HhsKod, string GkdYontem = "Y") : IRequest<CreateConsentResult>;

/// <param name="GkdUrl">Sadece Y: mobil bu adresi açar; adres tek kullanımlık drmKod'u içerir.</param>
public sealed record CreateConsentResult(Guid ConsentId, string GkdYontem, string? GkdUrl);

public sealed class CreateConsentValidator : AbstractValidator<CreateConsentCommand>
{
    public CreateConsentValidator()
    {
        RuleFor(x => x.HhsKod).NotEmpty().MaximumLength(8);
        RuleFor(x => x.GkdYontem).Must(y => y is "Y" or "A").WithMessage("GKD yöntemi 'Y' (yönlendirmeli) veya 'A' (ayrık) olmalı.");
    }
}

public sealed class CreateConsentHandler(
    IAppDbContext db,
    IHhsClient hhs,
    ISecretProtector protector,
    SubscriptionService subscriptions,
    TimeProvider clock,
    IOptions<HhsOptions> options) : IRequestHandler<CreateConsentCommand, CreateConsentResult>
{
    private readonly HhsOptions _o = options.Value;

    public async Task<CreateConsentResult> Handle(CreateConsentCommand request, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == request.UserId, ct);
        if (user.TcknEnc is null)
            throw AppProblemException.Conflict("tckn_required", "Banka bağlamak için önce kimlik numaranızı girin.");

        var bank = await db.Banks.AsNoTracking().FirstOrDefaultAsync(b => b.HhsKod == request.HhsKod && b.IsActive, ct)
            ?? throw new AppProblemException(404, "bank_not_found", "Banka bulunamadı.");

        var decoupled = request.GkdYontem == "A";
        if (decoupled && !bank.SupportsDecoupled)
            throw AppProblemException.BadRequest("decoupled_not_supported", "Bu banka uygulamasından onay (ayrık GKD) desteklemiyor.");

        var active = await db.Consents
            .Where(c => c.UserId == request.UserId && c.HhsKod == request.HhsKod && RizaDurumu.Aktif.Contains(c.RizaDrm))
            .ToListAsync(ct);
        if (active.Any(c => c.RizaDrm == RizaDurumu.YetkiKullanildi))
            throw AppProblemException.Conflict("consent_already_active", "Bu banka zaten bağlı.");

        // Ayrık GKD sonucu olayla gelir; HHS abonelik yoksa rızayı reddeder.
        if (decoupled)
            await subscriptions.EnsureAsync(request.HhsKod, ct);

        var now = clock.GetUtcNow();
        var son = now.AddDays(_o.ConsentDurationDays);
        var remote = await hhs.CreateConsentAsync(new HhsConsentRequest(
            request.HhsKod,
            protector.Unprotect(user.TcknEnc, SecretPurpose.Tckn),
            _o.IzinTurleri,
            son,
            now.AddDays(-_o.TransactionHistoryDays),
            son,
            _o.RedirectUri,
            decoupled), ct);

        // Yarım kalmış eski talepler: HHS yeni talebi açınca eskisini 01 ile iptal eder; yerelde de kapat.
        foreach (var old in active)
            old.TransitionTo(RizaDurumu.YetkiIptal, RizaIptalKodu.YeniRiza, now);
        await db.SaveChangesAsync(ct);

        var drmKod = decoupled ? null : Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(24));
        var consent = new Consent
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            HhsKod = request.HhsKod,
            RizaNo = remote.RizaNo,
            RizaDrm = remote.RizaDrm,
            IzinTurleri = [.. remote.IzinTurleri],
            ErisimIzniSonTrh = remote.ErisimIzniSonTrh,
            HesapIslBslZmn = remote.HesapIslemBslZmn,
            HesapIslBtsZmn = remote.HesapIslemBtsZmn,
            GkdYontem = request.GkdYontem,
            DrmKodHash = drmKod is null ? null : SecretHash.Sha256(drmKod),
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Consents.Add(consent);
        await db.SaveChangesAsync(ct);

        if (decoupled)
            return new CreateConsentResult(consent.Id, "A", null);

        var hhsYonAdr = remote.HhsYonAdr ?? throw new HhsException(502, null, "HHS yönlendirme adresi (hhsYonAdr) dönmedi.");
        var gkdUrl = $"{hhsYonAdr}{(hhsYonAdr.Contains('?') ? '&' : '?')}drmKod={drmKod}";
        return new CreateConsentResult(consent.Id, "Y", gkdUrl);
    }
}
