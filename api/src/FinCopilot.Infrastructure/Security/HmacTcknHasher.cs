using System.Security.Cryptography;
using System.Text;
using FinCopilot.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace FinCopilot.Infrastructure.Security;

/// <summary>appsettings "Security" bölümü. Üretimde ortam değişkeni: Security__TcknHashKey.</summary>
public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>
    /// TCKN arama özeti için HMAC anahtarı (en az 32 bayt). Değişirse mevcut tckn_hash değerleri
    /// yeniden hesaplanmalı (tckn_enc çözülerek).
    /// </summary>
    public string TcknHashKey { get; set; } = "";
}

/// <summary>
/// TCKN 11 haneli ve tahmin uzayı küçük (~10^9 geçerli değer): düz SHA-256 saniyeler içinde kırılır.
/// HMAC anahtarı veritabanında olmadığı için DB sızıntısında hash tek başına işe yaramaz.
/// </summary>
internal sealed class HmacTcknHasher(IOptions<SecurityOptions> options) : ITcknHasher
{
    private readonly byte[] _key = GetKey(options.Value.TcknHashKey);

    public string Hash(string tckn) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(_key, Encoding.ASCII.GetBytes(tckn)));

    private static byte[] GetKey(string key)
    {
        var bytes = Encoding.UTF8.GetBytes(key);
        if (bytes.Length < 32)
            throw new InvalidOperationException("Security:TcknHashKey en az 32 bayt olmalı (ortam değişkeni Security__TcknHashKey).");
        return bytes;
    }
}
