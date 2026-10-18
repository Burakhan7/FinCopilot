namespace FinCopilot.Application.Consents;

/// <summary>appsettings "Hhs" bölümü.</summary>
public sealed class HhsOptions
{
    public const string SectionName = "Hhs";

    /// <summary>Bizim YÖS kodumuz (Mock HHS'de 8001).</summary>
    public string YosKod { get; set; } = "8001";

    /// <summary>GKD sonrası bankanın kullanıcıyı döndüreceği adres (mobil deep link).</summary>
    public string RedirectUri { get; set; } = "fincopilot://gkd-donus";

    /// <summary>Bireysel rıza en fazla 6 ay.</summary>
    public int ConsentDurationDays { get; set; } = 180;

    /// <summary>İşlem geçmişi en fazla 12 ay geriye.</summary>
    public int TransactionHistoryDays { get; set; } = 365;

    /// <summary>Uygulamanın özellikleri tüm hesap ve kart izinlerini kullanır.</summary>
    public List<string> IzinTurleri { get; set; } = ["01", "02", "03", "04", "05", "06", "07", "08", "09"];

    /// <summary>Gerçek ortamda HHS-YÖS API'den gelecek; şimdilik buradan banks tablosuna yazılır.</summary>
    public List<HhsBankOptions> Banks { get; set; } = [];
}

public sealed class HhsBankOptions
{
    public string HhsKod { get; set; } = "";
    public string Name { get; set; } = "";
    public string ApiBaseUrl { get; set; } = "";
    public string? LogoUrl { get; set; }
    public bool SupportsDecoupled { get; set; }
}
