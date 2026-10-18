namespace FinCopilot.Application.Abstractions;

public interface ISmsSender
{
    Task SendAsync(string phoneE164, string message, CancellationToken cancellationToken);
}

public enum PinVerification
{
    Failed,
    Success,
    SuccessRehashNeeded,
}

public interface IPinHasher
{
    string Hash(string pin);
    PinVerification Verify(string hash, string pin);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    AccessToken CreateAccessToken(Guid userId, Guid sessionId);
    /// <summary>Kriptografik rastgele, URL güvenli refresh token.</summary>
    string CreateRefreshToken();
    TimeSpan RefreshTokenLifetime { get; }
}
