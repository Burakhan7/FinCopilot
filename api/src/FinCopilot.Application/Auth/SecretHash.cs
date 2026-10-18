using System.Security.Cryptography;
using System.Text;

namespace FinCopilot.Application.Auth;

/// <summary>
/// Yüksek entropili sırların (refresh token) ve kısa ömürlü, deneme sınırlı kodların (OTP) özetini alır.
/// PIN için kullanılmaz; PIN yavaş hash ister (<see cref="Abstractions.IPinHasher"/>).
/// </summary>
public static class SecretHash
{
    public static string Sha256(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));
}
