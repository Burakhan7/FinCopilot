using FinCopilot.Application.Abstractions;
using FinCopilot.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace FinCopilot.Infrastructure.Auth;

/// <summary>ASP.NET Core Identity PasswordHasher (PBKDF2-HMACSHA512, 100.000 iterasyon, sürümlü format).</summary>
internal sealed class PinHasher : IPinHasher
{
    private static readonly PasswordHasher<User> Hasher = new();
    private static readonly User Dummy = new() { Phone = "" };

    public string Hash(string pin) => Hasher.HashPassword(Dummy, pin);

    public PinVerification Verify(string hash, string pin) => Hasher.VerifyHashedPassword(Dummy, hash, pin) switch
    {
        PasswordVerificationResult.Success => PinVerification.Success,
        PasswordVerificationResult.SuccessRehashNeeded => PinVerification.SuccessRehashNeeded,
        _ => PinVerification.Failed,
    };
}
