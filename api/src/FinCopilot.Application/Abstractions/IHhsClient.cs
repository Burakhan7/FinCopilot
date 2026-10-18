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
}

public sealed record HhsConsentRequest(
    string HhsKod,
    string Tckn,
    IReadOnlyList<string> IzinTurleri,
    DateTimeOffset ErisimIzniSonTrh,
    DateTimeOffset HesapIslemBslZmn,
    DateTimeOffset HesapIslemBtsZmn,
    string YonAdr);

public sealed record HhsConsent(
    string RizaNo,
    string RizaDrm,
    string? RizaIptDtyKod,
    string? HhsYonAdr,
    IReadOnlyList<string> IzinTurleri,
    DateTimeOffset ErisimIzniSonTrh,
    DateTimeOffset? HesapIslemBslZmn,
    DateTimeOffset? HesapIslemBtsZmn);

public sealed record HhsToken(string AccessToken, TimeSpan AccessLifetime, string RefreshToken, TimeSpan RefreshLifetime);

/// <summary>HHS'nin hata yanıtı (ÖHVPS errorCode, örn. TR.OHVPS.Resource.ConsentRevoked).</summary>
public sealed class HhsException(int statusCode, string? errorCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string? ErrorCode { get; } = errorCode;

    public bool IsConsentRevoked => ErrorCode == "TR.OHVPS.Resource.ConsentRevoked";
}

public interface ITcknHasher
{
    /// <summary>Anahtarlı (HMAC) özet; anahtar olmadan TCKN'ye geri götürülemez.</summary>
    string Hash(string tckn);
}
