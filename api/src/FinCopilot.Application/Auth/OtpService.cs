using System.Security.Cryptography;
using FinCopilot.Application.Abstractions;
using FinCopilot.Application.Common;
using FinCopilot.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinCopilot.Application.Auth;

public sealed class OtpService(IAppDbContext db, ISmsSender sms, TimeProvider clock, IOptions<AuthOptions> options)
{
    private readonly AuthOptions _o = options.Value;

    public async Task<OtpChallenge> SendAsync(string phoneE164, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var recent = await db.OtpChallenges
            .Where(c => c.Phone == phoneE164 && c.CreatedAt > now.AddHours(-1))
            .Select(c => c.CreatedAt)
            .ToListAsync(ct);

        if (recent.Count > 0 && recent.Max() > now - _o.OtpResendCooldown)
            throw AppProblemException.TooManyRequests("otp_cooldown", "Yeni kod istemeden önce biraz bekleyin.");
        if (recent.Count >= _o.OtpMaxPerHour)
            throw AppProblemException.TooManyRequests("otp_rate_limited", "Çok fazla kod istendi. Daha sonra tekrar deneyin.");

        var code = _o.OtpFixedCode ?? RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var challenge = new OtpChallenge
        {
            Id = Guid.NewGuid(),
            Phone = phoneE164,
            CodeHash = "",
            CreatedAt = now,
            ExpiresAt = now + _o.OtpLifetime,
        };
        challenge.CodeHash = HashCode(challenge.Id, code);

        db.OtpChallenges.Add(challenge);
        await db.SaveChangesAsync(ct);
        await sms.SendAsync(phoneE164, $"FinCopilot doğrulama kodunuz: {code}. Kimseyle paylaşmayın.", ct);

        return challenge;
    }

    /// <summary>
    /// Kodu doğrular ve challenge'ı kullanılmış işaretler (kaydetmek çağıranın işi).
    /// Hatalı denemede sayaç hemen kaydedilir.
    /// </summary>
    public async Task VerifyAsync(Guid challengeId, string phoneE164, string code, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var challenge = await db.OtpChallenges.FirstOrDefaultAsync(c => c.Id == challengeId, ct);

        if (challenge is null || challenge.Phone != phoneE164 || challenge.ConsumedAt is not null
            || challenge.ExpiresAt <= now || challenge.Attempts >= _o.OtpMaxAttempts)
            throw Invalid();

        if (!SecretHash.FixedTimeEquals(challenge.CodeHash, HashCode(challenge.Id, code)))
        {
            challenge.Attempts++;
            await db.SaveChangesAsync(ct);
            throw Invalid();
        }

        challenge.ConsumedAt = now;
    }

    private static string HashCode(Guid challengeId, string code) => SecretHash.Sha256($"{challengeId:N}:{code}");

    private static AppProblemException Invalid() =>
        AppProblemException.BadRequest("otp_invalid", "Doğrulama kodu hatalı veya süresi dolmuş.");
}
