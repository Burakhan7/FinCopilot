using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FinCopilot.Application.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FinCopilot.Infrastructure.Auth;

/// <summary>appsettings "Jwt" bölümü. SigningKey üretimde ortam değişkeninden gelir (Jwt__SigningKey).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "fincopilot-api";
    public string Audience { get; set; } = "fincopilot-mobile";
    /// <summary>HS256 için en az 32 bayt.</summary>
    public string SigningKey { get; set; } = "";
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(30);

    public SymmetricSecurityKey GetSecurityKey()
    {
        var bytes = Encoding.UTF8.GetBytes(SigningKey);
        if (bytes.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey en az 32 bayt olmalı (ortam değişkeni Jwt__SigningKey).");
        return new SymmetricSecurityKey(bytes);
    }
}

public static class AppClaims
{
    public const string UserId = JwtRegisteredClaimNames.Sub;
    public const string SessionId = JwtRegisteredClaimNames.Sid;
}

internal sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    private readonly JwtOptions _o = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public TimeSpan RefreshTokenLifetime => _o.RefreshTokenLifetime;

    public AccessToken CreateAccessToken(Guid userId, Guid sessionId)
    {
        var now = clock.GetUtcNow();
        var expires = now + _o.AccessTokenLifetime;

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _o.Issuer,
            Audience = _o.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(AppClaims.UserId, userId.ToString()),
                new Claim(AppClaims.SessionId, sessionId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            ]),
            SigningCredentials = new SigningCredentials(_o.GetSecurityKey(), SecurityAlgorithms.HmacSha256),
        });

        return new AccessToken(token, expires);
    }

    public string CreateRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
}
