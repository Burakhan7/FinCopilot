using System.Text;
using FinCopilot.Application.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace FinCopilot.Infrastructure.Security;

/// <summary>
/// ASP.NET Data Protection (AES-256-CBC + HMACSHA256, anahtar rotasyonu otomatik).
/// Anahtar halkası data_protection_keys tablosunda; Api ve Worker aynı ApplicationName'i kullanır.
/// </summary>
internal sealed class DataProtectionSecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    // Amaç dizgeleri kalıcı veriye bağlıdır: değiştirilirse eski veri çözülemez.
    private readonly IDataProtector _bankToken = provider.CreateProtector("FinCopilot.BankToken.v1");
    private readonly IDataProtector _tckn = provider.CreateProtector("FinCopilot.Tckn.v1");
    private readonly IDataProtector _accountNumber = provider.CreateProtector("FinCopilot.AccountNumber.v1");

    public byte[] Protect(string plaintext, SecretPurpose purpose) =>
        For(purpose).Protect(Encoding.UTF8.GetBytes(plaintext));

    public string Unprotect(byte[] ciphertext, SecretPurpose purpose) =>
        Encoding.UTF8.GetString(For(purpose).Unprotect(ciphertext));

    private IDataProtector For(SecretPurpose purpose) => purpose switch
    {
        SecretPurpose.BankToken => _bankToken,
        SecretPurpose.Tckn => _tckn,
        SecretPurpose.AccountNumber => _accountNumber,
        _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null),
    };
}
