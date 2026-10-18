using FluentValidation;

namespace FinCopilot.Application.Auth;

public static class AuthValidationRules
{
    public static IRuleBuilderOptions<T, string> ValidPhone<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(p => PhoneNumber.TryNormalize(p, out _))
            .WithMessage("Geçerli bir cep telefonu numarası girin (5XX XXX XX XX).");

    public static IRuleBuilderOptions<T, string> ValidPin<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("PIN gerekli.")
            .Matches(@"^\d{6}$").WithMessage("PIN 6 haneli rakam olmalı.")
            .Must(p => !IsWeakPin(p)).WithMessage("PIN çok kolay tahmin edilebilir (aynı ya da ardışık rakamlar).");

    public static IRuleBuilderOptions<T, string> ValidOtpCode<T>(this IRuleBuilder<T, string> rule) =>
        rule.Matches(@"^\d{6}$").WithMessage("Doğrulama kodu 6 haneli olmalı.");

    public static IRuleBuilderOptions<T, string> ValidDeviceId<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(128);

    public static IRuleBuilderOptions<T, string> ValidPlatform<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(p => p is "android" or "ios").WithMessage("Platform 'android' veya 'ios' olmalı.");

    /// <summary>111111, 123456, 654321 gibi PIN'ler.</summary>
    public static bool IsWeakPin(string pin)
    {
        if (pin.Length < 2 || !pin.All(char.IsAsciiDigit))
            return false;

        bool allSame = true, ascending = true, descending = true;
        for (var i = 1; i < pin.Length; i++)
        {
            var diff = pin[i] - pin[i - 1];
            allSame &= diff == 0;
            ascending &= diff == 1;
            descending &= diff == -1;
        }
        return allSame || ascending || descending;
    }
}
