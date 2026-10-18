using FinCopilot.Application.Abstractions;
using FinCopilot.Application.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace FinCopilot.Application.Auth;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<AuthTokens>;

public sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(256);
}

public sealed class RefreshTokenHandler(IAppDbContext db, SessionIssuer sessions, TimeProvider clock)
    : IRequestHandler<RefreshTokenCommand, AuthTokens>
{
    public async Task<AuthTokens> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var hash = SecretHash.Sha256(request.RefreshToken);

        var session = await db.UserSessions.FirstOrDefaultAsync(s => s.RefreshTokenHash == hash, ct);
        if (session is null)
        {
            // Döndürülmüş eski bir token tekrar geldiyse token çalınmış olabilir: oturumu kapat.
            var reused = await db.UserSessions.FirstOrDefaultAsync(s => s.PreviousRefreshTokenHash == hash && s.RevokedAt == null, ct);
            if (reused is not null)
            {
                SessionIssuer.Revoke(reused, now, "reuse_detected");
                await db.SaveChangesAsync(ct);
            }
            throw Invalid();
        }

        if (session.RevokedAt is not null || session.ExpiresAt <= now)
            throw Invalid();

        var tokens = sessions.Rotate(session);
        await db.SaveChangesAsync(ct);
        return tokens;
    }

    private static AppProblemException Invalid() =>
        AppProblemException.Unauthorized("invalid_refresh_token", "Oturumun süresi doldu. Tekrar giriş yapın.");
}
