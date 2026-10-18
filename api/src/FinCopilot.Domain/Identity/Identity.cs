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
