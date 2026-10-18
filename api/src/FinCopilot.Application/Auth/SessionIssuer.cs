using FinCopilot.Application.Abstractions;
using FinCopilot.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace FinCopilot.Application.Auth;

public sealed record AuthTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

public sealed class SessionIssuer(IAppDbContext db, ITokenService tokens, TimeProvider clock)
{
    /// <summary>Cihazdaki eski oturumları kapatır, yenisini ekler (kaydetmek çağıranın işi).</summary>
    public async Task<AuthTokens> IssueAsync(Guid userId, Guid userDeviceId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        var previous = await db.UserSessions
            .Where(s => s.UserDeviceId == userDeviceId && s.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var s in previous)
            Revoke(s, now, "relogin");

        var refreshToken = tokens.CreateRefreshToken();
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserDeviceId = userDeviceId,
            RefreshTokenHash = SecretHash.Sha256(refreshToken),
            CreatedAt = now,
            ExpiresAt = now + tokens.RefreshTokenLifetime,
        };
        db.UserSessions.Add(session);

        var access = tokens.CreateAccessToken(userId, session.Id);
        return new AuthTokens(access.Token, access.ExpiresAt, refreshToken, session.ExpiresAt);
    }

    public AuthTokens Rotate(UserSession session)
    {
        var now = clock.GetUtcNow();
        var refreshToken = tokens.CreateRefreshToken();

        session.PreviousRefreshTokenHash = session.RefreshTokenHash;
        session.RefreshTokenHash = SecretHash.Sha256(refreshToken);
        session.LastUsedAt = now;
        session.ExpiresAt = now + tokens.RefreshTokenLifetime;

        var access = tokens.CreateAccessToken(session.UserId, session.Id);
        return new AuthTokens(access.Token, access.ExpiresAt, refreshToken, session.ExpiresAt);
    }

    public static void Revoke(UserSession session, DateTimeOffset now, string reason)
    {
        session.RevokedAt = now;
        session.RevokeReason = reason;
    }
}
