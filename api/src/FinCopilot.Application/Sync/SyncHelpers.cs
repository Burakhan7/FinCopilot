namespace FinCopilot.Application.Sync;

/// <summary>Türkiye 2016'dan beri sabit UTC+3 (yaz saati yok). ÖHVPS günlük kotası TR gününe göre sayılır.</summary>
public static class TurkeyTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(3);

    /// <summary>Verilen anın TR takvim gününün başlangıcı (00:00+03:00).</summary>
    public static DateTimeOffset StartOfDay(DateTimeOffset instant)
    {
        var local = instant.ToOffset(Offset);
        return new DateTimeOffset(local.Year, local.Month, local.Day, 0, 0, 0, Offset);
    }
}

public static class SyncWindows
{
    /// <summary>[start, end) aralığını en fazla <paramref name="size"/> uzunluğunda, ardışık pencerelere böler.</summary>
    public static IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> Split(DateTimeOffset start, DateTimeOffset end, TimeSpan size)
    {
        for (var s = start; s < end; s += size)
            yield return (s, s + size < end ? s + size : end);
    }
}

public static class Masking
{
    /// <summary>"TR250900205202796328220556" → "TR25 **** 0556".</summary>
    public static string? Iban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
            return null;
        var s = iban.Replace(" ", "");
        return s.Length <= 8 ? new string('*', s.Length) : $"{s[..4]} **** {s[^4..]}";
    }

    public static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
