namespace FinCopilot.Application.Auth;

/// <summary>Türkiye cep telefonu numaralarını E.164 biçimine (+905XXXXXXXXX) çevirir.</summary>
public static class PhoneNumber
{
    public static bool TryNormalize(string? input, out string e164)
    {
        e164 = "";
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var digits = new string(input.Where(c => !char.IsWhiteSpace(c) && c is not ('-' or '(' or ')')).ToArray());
        if (digits.StartsWith("+90")) digits = digits[3..];
        else if (digits.StartsWith("0090")) digits = digits[4..];
        else if (digits.StartsWith("90") && digits.Length == 12) digits = digits[2..];
        else if (digits.StartsWith('0') && digits.Length == 11) digits = digits[1..];

        if (digits.Length != 10 || digits[0] != '5' || !digits.All(char.IsAsciiDigit))
            return false;

        e164 = "+90" + digits;
        return true;
    }

    public static string Normalize(string input) =>
        TryNormalize(input, out var e164) ? e164 : throw new ArgumentException("Geçersiz telefon numarası.", nameof(input));
}
