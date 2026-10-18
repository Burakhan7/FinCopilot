using System.Security.Cryptography;
using System.Text;
using FinCopilot.Application.Abstractions;
using FinCopilot.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;

namespace FinCopilot.UnitTests.Security;

public class DataProtectionSecretProtectorTests
{
    private readonly DataProtectionSecretProtector _sut = new(new EphemeralDataProtectionProvider());

    [Fact]
    public void Protect_then_unprotect_returns_original()
    {
        const string token = "eyJhbGciOi.erisim-belirteci.örnek";

        var cipher = _sut.Protect(token, SecretPurpose.BankToken);

        Assert.DoesNotContain(token, Encoding.UTF8.GetString(cipher));
        Assert.Equal(token, _sut.Unprotect(cipher, SecretPurpose.BankToken));
    }

    [Fact]
    public void Ciphertext_of_one_purpose_cannot_be_read_with_another()
    {
        var cipher = _sut.Protect("12345678901", SecretPurpose.Tckn);

        Assert.ThrowsAny<CryptographicException>(() => _sut.Unprotect(cipher, SecretPurpose.BankToken));
    }

    [Fact]
    public void Tampered_ciphertext_is_rejected()
    {
        var cipher = _sut.Protect("TR000000000000000000000000", SecretPurpose.AccountNumber);
        cipher[^1] ^= 0xFF;

        Assert.ThrowsAny<CryptographicException>(() => _sut.Unprotect(cipher, SecretPurpose.AccountNumber));
    }
}
