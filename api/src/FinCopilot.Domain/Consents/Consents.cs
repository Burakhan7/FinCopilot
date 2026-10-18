namespace FinCopilot.Domain.Consents;

/// <summary>ÖHVPS rıza durumu (rizaDrm) kodları.</summary>
public static class RizaDurumu
{
    public const string YetkiBekleniyor = "B";
    public const string Yetkilendirildi = "Y";
    public const string YetkiKullanildi = "K";
    public const string YetkiSonlandirildi = "S";
    public const string YetkiIptal = "I";

    /// <summary>Bir kullanıcının bir bankada aynı anda tek rızası bu durumlarda olabilir.</summary>
    public static readonly IReadOnlyList<string> Aktif = [YetkiBekleniyor, Yetkilendirildi, YetkiKullanildi];
}

public class Bank
{
    public required string HhsKod { get; set; }
    public required string Name { get; set; }
    public string? LogoUrl { get; set; }
    public required string ApiBaseUrl { get; set; }
    public bool SupportsDecoupled { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Consent
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string HhsKod { get; set; }
    /// <summary>HHS'den dönene kadar boş.</summary>
    public string? RizaNo { get; set; }
    public string RizaDrm { get; set; } = RizaDurumu.YetkiBekleniyor;
    public string? RizaIptDtyKod { get; set; }
    /// <summary>izinTur kodları, örn. ["01","03"].</summary>
    public List<string> IzinTurleri { get; set; } = [];
    public DateTimeOffset? ErisimIzniSonTrh { get; set; }
    public DateTimeOffset? HesapIslBslZmn { get; set; }
    public DateTimeOffset? HesapIslBtsZmn { get; set; }
    /// <summary>Y: yönlendirmeli, A: ayrık.</summary>
    public string GkdYontem { get; set; } = "Y";
    public string? DrmKodHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ConsentToken? Token { get; set; }
}

public class ConsentToken
{
    public Guid ConsentId { get; set; }
    public required byte[] AccessTokenEnc { get; set; }
    public DateTimeOffset AccessExpiresAt { get; set; }
    public byte[]? RefreshTokenEnc { get; set; }
    public DateTimeOffset? RefreshExpiresAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class SyncJob
{
    public Guid Id { get; set; }
    public Guid ConsentId { get; set; }
    /// <summary>hesaplar, bakiye, islemler, kartlar, kart-detay, kart-hareketleri</summary>
    public required string Resource { get; set; }
    /// <summary>E: kullanıcı tetikli, H: sistem tetikli (günde ≤4).</summary>
    public required string PsuInitiated { get; set; }
    public DateTimeOffset? WindowStart { get; set; }
    public DateTimeOffset? WindowEnd { get; set; }
    public required string Status { get; set; }
    public int? HttpStatus { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}

public class EventInboxItem
{
    public Guid Id { get; set; }
    public required string OlayNo { get; set; }
    public required string OlayTipi { get; set; }
    public string? KaynakTipi { get; set; }
    public string? KaynakNo { get; set; }
    public required string Payload { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
