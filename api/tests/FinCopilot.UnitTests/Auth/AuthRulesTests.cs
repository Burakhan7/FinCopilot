using FinCopilot.Application.Auth;

namespace FinCopilot.UnitTests.Auth;

public class AuthRulesTests
{
    [Theory]
    [InlineData("5321234567")]
    [InlineData("05321234567")]
    [InlineData("+905321234567")]
    [InlineData("905321234567")]
    [InlineData("00905321234567")]
    [InlineData("0532 123 45 67")]
    [InlineData("(532) 123-45-67")]
    public void Turkish_mobile_numbers_normalize_to_e164(string input)
    {
        Assert.True(PhoneNumber.TryNormalize(input, out var e164));
        Assert.Equal("+905321234567", e164);
    }

    [Theory]
    [InlineData("")]
    [InlineData("2161234567")]   // sabit hat
    [InlineData("532123456")]    // eksik hane
    [InlineData("+15321234567")] // yabancı
    [InlineData("53212345ab")]
    public void Invalid_numbers_are_rejected(string input) =>
        Assert.False(PhoneNumber.TryNormalize(input, out _));

    [Theory]
    [InlineData("111111", true)]
    [InlineData("123456", true)]
    [InlineData("654321", true)]
    [InlineData("012345", true)]
    [InlineData("482915", false)]
    [InlineData("112233", false)]
    public void Weak_pins_are_detected(string pin, bool weak) =>
        Assert.Equal(weak, AuthValidationRules.IsWeakPin(pin));

    [Fact]
    public void Secret_hash_is_stable_and_hex()
    {
        var h = SecretHash.Sha256("abc");
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", h);
        Assert.True(SecretHash.FixedTimeEquals(h, SecretHash.Sha256("abc")));
    }
}
