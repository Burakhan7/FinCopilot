using FinCopilot.Application.Abstractions;
using FinCopilot.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FinCopilot.UnitTests.Auth;

public class AuthInfrastructureTests
{
    private static readonly JwtOptions Jwt = new() { SigningKey = new string('k', 32) };

    [Fact]
    public void Pin_hash_verifies_only_the_right_pin()
    {
        var hasher = new PinHasher();
        var hash = hasher.Hash("482915");

        Assert.DoesNotContain("482915", hash);
        Assert.Equal(PinVerification.Success, hasher.Verify(hash, "482915"));
        Assert.Equal(PinVerification.Failed, hasher.Verify(hash, "482916"));
    }

    [Fact]
    public async Task Access_token_carries_user_and_session_and_validates()
    {
        var sut = new JwtTokenService(Options.Create(Jwt), TimeProvider.System);
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var token = sut.CreateAccessToken(userId, sessionId);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, new TokenValidationParameters
        {
            ValidIssuer = Jwt.Issuer,
            ValidAudience = Jwt.Audience,
            IssuerSigningKey = Jwt.GetSecurityKey(),
        });
        Assert.True(result.IsValid);
        Assert.Equal(userId.ToString(), result.Claims[AppClaims.UserId]);
        Assert.Equal(sessionId.ToString(), result.Claims[AppClaims.SessionId]);
    }

    [Fact]
    public void Refresh_tokens_are_random_and_long()
    {
        var sut = new JwtTokenService(Options.Create(Jwt), TimeProvider.System);

        var a = sut.CreateRefreshToken();

        Assert.NotEqual(a, sut.CreateRefreshToken());
        Assert.True(a.Length >= 43);
    }

    [Fact]
    public void Short_signing_key_is_rejected()
    {
        var sut = new JwtTokenService(Options.Create(new JwtOptions { SigningKey = "kisa" }), TimeProvider.System);

        Assert.Throws<InvalidOperationException>(() => sut.CreateAccessToken(Guid.NewGuid(), Guid.NewGuid()));
    }
}
