namespace FinCopilot.Application.Users;

public static class Tckn
{
    /// <summary>T.C. kimlik numarası algoritması: 11 hane, ilk hane 0 değil, 10. ve 11. hane kontrol hanesi.</summary>
    public static bool IsValid(string? tckn)
    {
        if (tckn is null || tckn.Length != 11 || tckn[0] == '0' || !tckn.All(char.IsAsciiDigit))
            return false;

        var d = tckn.Select(c => c - '0').ToArray();
        var odd = d[0] + d[2] + d[4] + d[6] + d[8];
        var even = d[1] + d[3] + d[5] + d[7];
        var d10 = ((odd * 7 - even) % 10 + 10) % 10;
        var d11 = d[..10].Sum() % 10;
        return d[9] == d10 && d[10] == d11;
    }
}
