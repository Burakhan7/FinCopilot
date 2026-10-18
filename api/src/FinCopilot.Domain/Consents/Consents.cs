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

    public static bool IsTerminal(string drm) => drm is YetkiSonlandirildi or YetkiIptal;

    /// <summary>
    /// İzinli geçişler: B → Y/K/I, Y → K/I, K → S/I. S ve I son durumdur.
    /// (B → K: yönlendirmeli akışta Y'yi yerelde görmeden token alınır.)
    /// </summary>
    public static bool CanTransition(string from, string to) => (from, to) switch
    {
        (YetkiBekleniyor, Yetkilendirildi or YetkiKullanildi or YetkiIptal) => true,
        (Yetkilendirildi, YetkiKullanildi or YetkiIptal) => true,
        (YetkiKullanildi, YetkiSonlandirildi or YetkiIptal) => true,
        _ => false,
    };

    public static string Aciklama(string drm) => drm switch
    {
        YetkiBekleniyor => "Banka onayı bekleniyor",
        Yetkilendirildi => "Bankada onaylandı",
        YetkiKullanildi => "Bağlı",
        YetkiSonlandirildi => "Süresi doldu",
        YetkiIptal => "İptal edildi",
        _ => drm,
    };
}

/// <summary>ÖHVPS rizaIptDtyKod değerleri (bkz. docs/ohvps-hesap-bilgisi-referans.md §4).</summary>
public static class RizaIptalKodu
{
    public const string YeniRiza = "01";
    public const string KullaniciHhsUzerinden = "02";
    public const string KullaniciYosUzerinden = "03";

    public static string Aciklama(string? kod) => kod switch
    {
        null => "",
        "01" => "Yeni rıza talebi ile iptal",
        "02" => "Banka üzerinden iptal edildi",
        "03" => "Uygulama üzerinden iptal edildi",
        "04" or "05" or "06" => "Süre aşımı",
        "07" => "Mükerrer çağrı",
        "08" => "Bankaya girilen TCKN uyuşmadı",
        "09" => "Bankada uygun hesap/kart yok",
        "10" => "Banka kanalı kapalı",
        "11" => "Hesap yetki sorunu",
        "12" => "Banka kontrolleri aşılamadı",
        "13" => "Bankada vazgeçildi",
        "14" => "Güvenlik şüphesi",
        "15" => "Rıza güncellendi",
        _ => "Diğer",
    };
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

    public bool IsActive => !RizaDurumu.IsTerminal(RizaDrm);

    /// <summary>
    /// Durumu değiştirir. Aynı duruma geçiş yok sayılır; izinsiz geçiş hata fırlatır
    /// (HHS'den gelen beklenmedik bir durum sessizce yazılmasın).
    /// </summary>
    /// <returns>Durum değiştiyse true.</returns>
    public bool TransitionTo(string drm, string? iptalKodu, DateTimeOffset now)
    {
        if (drm == RizaDrm)
            return false;
        if (!RizaDurumu.CanTransition(RizaDrm, drm))
            throw new InvalidOperationException($"Rıza durumu {RizaDrm} → {drm} geçişi geçersiz.");

        RizaDrm = drm;
        RizaIptDtyKod = drm == RizaDurumu.YetkiIptal ? iptalKodu : null;
        UpdatedAt = now;
        if (RizaDurumu.IsTerminal(drm))
            DrmKodHash = null;
        return true;
    }
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

/// <summary>Bir rıza için tek senkron çalıştırması. Sistem tetikli günlük kota bu kayıtlardan sayılır.</summary>
public class SyncJob
{
    public Guid Id { get; set; }
    public Guid ConsentId { get; set; }
    /// <summary><see cref="SyncKind"/></summary>
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

public static class SyncKind
{
    /// <summary>Bağlanınca 12 aylık ilk yükleme (PSU-Initiated=E).</summary>
    public const string Backfill = "backfill";
    /// <summary>Günde 4 sabit saatte (PSU-Initiated=H, 24 saat penceresi).</summary>
    public const string Scheduled = "scheduled";
}

public static class SyncStatus
{
    public const string Running = "running";
    public const string Succeeded = "succeeded";
    /// <summary>Geçici hata (ağ, 5xx); Hangfire yeniden dener.</summary>
    public const string Failed = "failed";
    /// <summary>Günlük sistem sorgu kotası dolu; denenmedi veya HHS reddetti.</summary>
    public const string Quota = "quota";
    /// <summary>Rıza artık aktif değil; senkron durdu.</summary>
    public const string Revoked = "revoked";
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
