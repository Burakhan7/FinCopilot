namespace FinCopilot.Application.Abstractions;

/// <summary>Her amaç ayrı anahtar türetir; bir amaçla şifrelenen veri diğeriyle çözülemez.</summary>
public enum SecretPurpose
{
    BankToken,
    Tckn,
    AccountNumber,
}

/// <summary>Veritabanındaki *_enc sütunları için şifreleme. Uygulaması Infrastructure'da.</summary>
public interface ISecretProtector
{
    byte[] Protect(string plaintext, SecretPurpose purpose);
    string Unprotect(byte[] ciphertext, SecretPurpose purpose);
}
