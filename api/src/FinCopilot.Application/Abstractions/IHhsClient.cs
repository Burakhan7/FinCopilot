namespace FinCopilot.Application.Abstractions;

/// <summary>
/// ÖHVPS Hesap Bilgisi Hizmeti adapter'ı. Bitirmede MockHhsClient; gerçek ortama geçiş yeni bir uygulama ile olur.
/// Bu arayüz ÖHVPS JSON'unu değil, uygulamanın ihtiyaç duyduğu alanları taşır.
/// </summary>
public interface IHhsClient
{
    Task<HhsConsent> CreateConsentAsync(HhsConsentRequest request, CancellationToken ct);
    Task<HhsConsent> GetConsentAsync(string hhsKod, string rizaNo, CancellationToken ct);
    /// <summary>K durumundaki rıza için erişim belirteci gerekir; B/Y için null geçilebilir.</summary>
    Task RevokeConsentAsync(string hhsKod, string rizaNo, string? accessToken, CancellationToken ct);
    Task<HhsToken> ExchangeAuthCodeAsync(string hhsKod, string rizaNo, string yetKod, CancellationToken ct);
    Task<HhsToken> RefreshAccessTokenAsync(string hhsKod, string rizaNo, string refreshToken, CancellationToken ct);

    // ---- Veri uçları (rıza K olmalı). Sadece kullanıcının bankada seçtiği hesap/kartlar döner.

    /// <summary>İzin 01 (+02 ile açılış tarihi).</summary>
    Task<IReadOnlyList<HhsAccount>> GetAccountsAsync(HhsAccess access, CancellationToken ct);

    /// <summary>İzin 03.</summary>
    Task<IReadOnlyList<HhsBalance>> GetBalancesAsync(HhsAccess access, CancellationToken ct);

    /// <summary>İzin 03, tek hesap (GET /hesaplar/{hspRef}/bakiye). /bakiye'den ayrı yol olduğu için kotası ayrı sayılır.</summary>
    Task<HhsBalance> GetBalanceAsync(HhsAccess access, string hspRef, CancellationToken ct);

    /// <summary>
    /// İzin 04 (+05 ile amaç kodu, açıklama ve karşı taraf). Pencere: kullanıcı tetikli ≤1 ay, sistem tetikli ≤24 saat.
    /// Tek sayfa döner; sistem tetikli sorguda her sayfa günlük kotadan düşer.
    /// </summary>
    Task<HhsPage<HhsTransaction>> GetTransactionsAsync(HhsAccess access, string hspRef, HhsWindow window, int page, int pageSize, CancellationToken ct);

    /// <summary>İzin 07.</summary>
    Task<IReadOnlyList<HhsCard>> GetCardsAsync(HhsAccess access, CancellationToken ct);

    /// <summary>İzin 08; sadece kredi kartları.</summary>
    Task<HhsCardDetail> GetCardDetailAsync(HhsAccess access, string kartRef, CancellationToken ct);

    /// <summary>İzin 09; pencere kuralları işlemlerle aynı.</summary>
    Task<HhsPage<HhsCardTransaction>> GetCardTransactionsAsync(HhsAccess access, string kartRef, HhsWindow window, int page, int pageSize, CancellationToken ct);

    // ---- Olay aboneliği (banka başına tek abonelik)

    /// <summary>Yoksa null.</summary>
    Task<HhsSubscription?> GetSubscriptionAsync(string hhsKod, CancellationToken ct);
    Task<HhsSubscription> CreateSubscriptionAsync(string hhsKod, IReadOnlyList<HhsEventType> types, CancellationToken ct);
    Task<HhsSubscription> UpdateSubscriptionAsync(string hhsKod, string olayAbonelikNo, IReadOnlyList<HhsEventType> types, CancellationToken ct);
}

public sealed record HhsEventType(string OlayTipi, string KaynakTipi);

public sealed record HhsSubscription(string OlayAbonelikNo, IReadOnlyList<HhsEventType> Types);

/// <param name="PsuInitiated">true: kullanıcı tetikli (E), false: sistem tetikli (H, günde 4 kota).</param>
public sealed record HhsAccess(string HhsKod, string AccessToken, bool PsuInitiated);

public sealed record HhsWindow(DateTimeOffset Start, DateTimeOffset End);

public sealed record HhsPage<T>(IReadOnlyList<T> Items, int TotalCount, int TotalPages);

public sealed record HhsAccount(
    string HspRef,
    string? HspNo,
    string? HspShb,
    string? SubeAdi,
    string? KisaAd,
    string ParaBirimi,
    string? HspTur,
    string? HspTip,
    string? UrunAdi,
    string? HspDrm,
    DateTimeOffset? AcilisTrh);

public sealed record HhsBalance(
    string HspRef,
    decimal BkyTtr,
    decimal? BlkTtr,
    string ParaBirimi,
    DateTimeOffset BkyZmn,
    decimal? KulKrdTtr,
    string? KrdDhlGstr);

/// <param name="Tutar">Her zaman pozitif; yön <paramref name="BrcAlc"/> ile (B borç, A alacak).</param>
public sealed record HhsTransaction(
    string IslNo,
    string? RefNo,
    decimal Tutar,
    string ParaBirimi,
    DateTimeOffset Zaman,
    string? Kanal,
    string BrcAlc,
    string? IslTur,
    string? IslAmc,
    string? Aciklama,
    string? OhkMsj,
    string? KarsiUnvan,
    string? KarsiIban);

public sealed record HhsCard(
    string KartRef,
    string? SonNo,
    string KartTipi,
    string? AltKartTipi,
    string? KartFormu,
    string? KartTuru,
    string? KartStatu,
    string? KartSema,
    string? UrunAdi);

/// <summary>Son kesilen ekstre + limit. ⚠ Alan adları spesifikasyondan doğrulanmadı (README).</summary>
public sealed record HhsCardDetail(
    string KartRef,
    DateOnly? HesapKesimTarihi,
    DateOnly? SonOdemeTarihi,
    decimal DonemBorcu,
    decimal AsgariOdeme,
    decimal? Limit,
    decimal? KullanilanLimit,
    string ParaBirimi);

public sealed record HhsFx(decimal OrijinalTutar, string OrijinalParaBirimi, decimal Kur);

/// <summary>⚠ Alan adları spesifikasyondan doğrulanmadı (README).</summary>
public sealed record HhsCardTransaction(
    string IslemNo,
    DateTimeOffset Tarih,
    decimal Tutar,
    string ParaBirimi,
    string BrcAlc,
    string? IslemTuru,
    string? Aciklama,
    string? UyeIsyeri,
    string? ReferansNo,
    int? TaksitSayisi,
    HhsFx? Doviz);

/// <param name="Decoupled">true: ayrık GKD (yetYntm=A, kullanıcı TCKN ile bankanın kendi uygulamasında onaylar).</param>
public sealed record HhsConsentRequest(
    string HhsKod,
    string Tckn,
    IReadOnlyList<string> IzinTurleri,
    DateTimeOffset ErisimIzniSonTrh,
    DateTimeOffset HesapIslemBslZmn,
    DateTimeOffset HesapIslemBtsZmn,
    string YonAdr,
    bool Decoupled = false);

/// <param name="YetKod">
/// Ayrık GKD onaylanınca (durum Y) rıza sorgusunda dönen yetki kodu.
/// ⚠ Mock HHS davranışı (gkd.yetKod); spesifikasyondan doğrulanmalı (README).
/// </param>
public sealed record HhsConsent(
    string RizaNo,
    string RizaDrm,
    string? RizaIptDtyKod,
    string? HhsYonAdr,
    IReadOnlyList<string> IzinTurleri,
    DateTimeOffset ErisimIzniSonTrh,
    DateTimeOffset? HesapIslemBslZmn,
    DateTimeOffset? HesapIslemBtsZmn,
    string? YetKod = null);

public sealed record HhsToken(string AccessToken, TimeSpan AccessLifetime, string RefreshToken, TimeSpan RefreshLifetime);

/// <summary>HHS'nin hata yanıtı (ÖHVPS errorCode, örn. TR.OHVPS.Resource.ConsentRevoked).</summary>
public sealed class HhsException(int statusCode, string? errorCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string? ErrorCode { get; } = errorCode;

    public bool IsConsentRevoked => ErrorCode == "TR.OHVPS.Resource.ConsentRevoked";

    /// <summary>Sistem tetikli günlük sorgu kotası doldu.</summary>
    public bool IsRateExceeded => ErrorCode == "TR.OHVPS.Connection.ExceededRate";
}

public interface ITcknHasher
{
    /// <summary>Anahtarlı (HMAC) özet; anahtar olmadan TCKN'ye geri götürülemez.</summary>
    string Hash(string tckn);
}
