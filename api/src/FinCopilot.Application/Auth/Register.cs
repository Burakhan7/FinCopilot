using FinCopilot.Application.Abstractions;
using FinCopilot.Application.Common;
using FinCopilot.Domain.Identity;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FinCopilot.Application.Auth;

public sealed record RegisterCommand(
    string Phone,
    Guid ChallengeId,
    string Code,
    string FullName,
    string Pin,
    string DeviceId,
    string Platform) : IRequest<AuthTokens>;

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Phone).ValidPhone();
        RuleFor(x => x.ChallengeId).NotEmpty();
        RuleFor(x => x.Code).ValidOtpCode();
        RuleFor(x => x.FullName).NotEmpty().MinimumLength(2).MaximumLength(200);
        RuleFor(x => x.Pin).ValidPin();
        RuleFor(x => x.DeviceId).ValidDeviceId();
        RuleFor(x => x.Platform).ValidPlatform();
    }
}

public sealed class RegisterHandler(
    IAppDbContext db,
    OtpService otp,
    SessionIssuer sessions,
    IPinHasher pinHasher,
    TimeProvider clock) : IRequestHandler<RegisterCommand, AuthTokens>
{
    public async Task<AuthTokens> Handle(RegisterCommand request, CancellationToken ct)
    {
        var phone = PhoneNumber.Normalize(request.Phone);

        // Önce telefonun sahibi olduğunu kanıtlasın, sonra kayıtlı olup olmadığını söyleyelim.
        await otp.VerifyAsync(request.ChallengeId, phone, request.Code, ct);

        if (await db.Users.AnyAsync(u => u.Phone == phone, ct))
            throw AppProblemException.Conflict("phone_already_registered", "Bu telefon numarası zaten kayıtlı. Giriş yapın.");

        var now = clock.GetUtcNow();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Phone = phone,
            FullName = request.FullName.Trim(),
            PinHash = pinHasher.Hash(request.Pin),
            CreatedAt = now,
            Settings = new UserSettings(),
        };
        var device = new UserDevice
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DeviceId = request.DeviceId,
            Platform = request.Platform,
            LastSeenAt = now,
        };
        db.Users.Add(user);
        db.UserDevices.Add(device);

        var tokens = await sessions.IssueAsync(user.Id, device.Id, ct);
        await db.SaveChangesAsync(ct);
        return tokens;
    }
}
