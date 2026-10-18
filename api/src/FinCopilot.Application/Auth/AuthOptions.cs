namespace FinCopilot.Application.Auth;

/// <summary>appsettings "Auth" bölümü.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public TimeSpan OtpLifetime { get; set; } = TimeSpan.FromMinutes(3);
    public int OtpMaxAttempts { get; set; } = 5;
    public TimeSpan OtpResendCooldown { get; set; } = TimeSpan.FromSeconds(60);
    public int OtpMaxPerHour { get; set; } = 5;

    /// <summary>Sadece Development/Testing: her SMS kodu bu olur. Üretimde boş olmalı (Api açılışta kontrol eder).</summary>
    public string? OtpFixedCode { get; set; }

    public int MaxPinAttempts { get; set; } = 5;
    public TimeSpan PinLockoutDuration { get; set; } = TimeSpan.FromMinutes(15);
}
