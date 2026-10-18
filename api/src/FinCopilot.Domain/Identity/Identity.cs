namespace FinCopilot.Domain.Identity;

public class User
{
    public Guid Id { get; set; }
    public required string Phone { get; set; }
    public byte[]? TcknEnc { get; set; }
    /// <summary>Arama için TCKN'nin anahtarlı özeti (şifreli değer aranamaz).</summary>
    public string? TcknHash { get; set; }
    public string? FullName { get; set; }
    public string? PinHash { get; set; }
    public int FailedPinAttempts { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public UserSettings? Settings { get; set; }
    public List<UserDevice> Devices { get; set; } = [];
}

public class UserDevice
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string DeviceId { get; set; }
    /// <summary>android / ios</summary>
    public required string Platform { get; set; }
    public string? FcmToken { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
}

/// <summary>SMS ile gönderilen tek kullanımlık kod. Kodun kendisi saklanmaz, özeti saklanır.</summary>
public class OtpChallenge
{
    public Guid Id { get; set; }
    /// <summary>E.164, örn. +905321234567</summary>
    public required string Phone { get; set; }
    public required string CodeHash { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}

/// <summary>Cihaz başına oturum; refresh token her kullanımda döndürülür.</summary>
public class UserSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid UserDeviceId { get; set; }
    public required string RefreshTokenHash { get; set; }
    /// <summary>Bir önceki token tekrar gelirse token çalınmış sayılır ve oturum iptal edilir.</summary>
    public string? PreviousRefreshTokenHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokeReason { get; set; }
}

public class UserSettings
{
    public Guid UserId { get; set; }
    public int? PaydayDay { get; set; }
    /// <summary>Yıllık oranlar; boşsa TCMB azami oranı kullanılır.</summary>
    public decimal? KmhRate { get; set; }
    public decimal? CardInterestRate { get; set; }
    public decimal? LateInterestRate { get; set; }
    public string Timezone { get; set; } = "Europe/Istanbul";
    public bool NotifyDailyBrief { get; set; } = true;
    public bool NotifyAlerts { get; set; } = true;
}
