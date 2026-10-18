using FinCopilot.Application.Abstractions;
using FinCopilot.Application.Common;
using FinCopilot.Domain.Identity;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinCopilot.Application.Auth;

/// <summary>
/// Kayıtlı cihazda telefon + PIN yeterli. Yeni cihazda ayrıca SMS kodu (ChallengeId + Code) gerekir;
/// yoksa 403 device_verification_required döner ve istemci önce /auth/otp çağırır.
/// </summary>
public sealed record LoginCommand(
    string Phone,
    string Pin,
    string DeviceId,
    string Platform,
    Guid? ChallengeId = null,
    string? Code = null) : IRequest<AuthTokens>;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Phone).ValidPhone();
        RuleFor(x => x.Pin).NotEmpty();
        RuleFor(x => x.DeviceId).ValidDeviceId();
        RuleFor(x => x.Platform).ValidPlatform();
        When(x => x.ChallengeId is not null || x.Code is not null, () =>
        {
            RuleFor(x => x.ChallengeId).NotEmpty();
            RuleFor(x => x.Code!).ValidOtpCode();
        });
    }
}

public sealed class LoginHandler(
    IAppDbContext db,
    OtpService otp,
    SessionIssuer sessions,
    IPinHasher pinHasher,
    TimeProvider clock,
    IOptions<AuthOptions> options) : IRequestHandler<LoginCommand, AuthTokens>
{
    private readonly AuthOptions _o = options.Value;

    public async Task<AuthTokens> Handle(LoginCommand request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var phone = PhoneNumber.Normalize(request.Phone);

        // Numaranın kayıtlı olup olmadığını sızdırmamak için tek tip hata.
        var user = await db.Users.FirstOrDefaultAsync(u => u.Phone == phone, ct)
            ?? throw InvalidCredentials();

        if (user.LockedUntil > now)
            throw Locked();

        await VerifyPinAsync(user, request.Pin, now, ct);

        var device = await db.UserDevices.FirstOrDefaultAsync(d => d.UserId == user.Id && d.DeviceId == request.DeviceId, ct);
        if (device is null)
        {
            if (request.ChallengeId is null || request.Code is null)
                throw AppProblemException.Forbidden("device_verification_required", "Yeni cihaz: SMS ile doğrulama gerekli.");

            await otp.VerifyAsync(request.ChallengeId.Value, phone, request.Code, ct);
            device = new UserDevice { Id = Guid.NewGuid(), UserId = user.Id, DeviceId = request.DeviceId, Platform = request.Platform };
            db.UserDevices.Add(device);
        }
        device.Platform = request.Platform;
        device.LastSeenAt = now;

        var tokens = await sessions.IssueAsync(user.Id, device.Id, ct);
        await db.SaveChangesAsync(ct);
        return tokens;
    }

    private async Task VerifyPinAsync(User user, string pin, DateTimeOffset now, CancellationToken ct)
    {
        var result = user.PinHash is null ? PinVerification.Failed : pinHasher.Verify(user.PinHash, pin);

        if (result == PinVerification.Failed)
        {
            user.FailedPinAttempts++;
            var lockedNow = user.FailedPinAttempts >= _o.MaxPinAttempts;
            if (lockedNow)
            {
                user.LockedUntil = now + _o.PinLockoutDuration;
                user.FailedPinAttempts = 0;
            }
            await db.SaveChangesAsync(ct);
            throw lockedNow ? Locked() : InvalidCredentials();
        }

        if (result == PinVerification.SuccessRehashNeeded)
            user.PinHash = pinHasher.Hash(pin);
        user.FailedPinAttempts = 0;
        user.LockedUntil = null;
    }

    private static AppProblemException InvalidCredentials() =>
        AppProblemException.Unauthorized("invalid_credentials", "Telefon numarası veya PIN hatalı.");

    private AppProblemException Locked() =>
        AppProblemException.Locked("account_locked", $"Çok fazla hatalı PIN. {_o.PinLockoutDuration.TotalMinutes:0} dakika sonra tekrar deneyin.");
}
