using FinCopilot.Application.Sync;

namespace FinCopilot.UnitTests.Sync;

public class SyncHelpersTests
{
    private static readonly TimeSpan Tr = TimeSpan.FromHours(3);

    [Fact]
    public void Windows_cover_range_without_gaps_and_respect_size()
    {
        var start = new DateTimeOffset(2025, 10, 18, 10, 0, 0, Tr);
        var end = start.AddDays(365);

        var windows = SyncWindows.Split(start, end, TimeSpan.FromDays(30)).ToList();

        Assert.Equal(13, windows.Count);
        Assert.Equal(start, windows[0].Start);
        Assert.Equal(end, windows[^1].End);
        Assert.All(windows.Zip(windows.Skip(1)), p => Assert.Equal(p.First.End, p.Second.Start));
        Assert.All(windows, w => Assert.True(w.End - w.Start <= TimeSpan.FromDays(30)));
    }

    [Fact]
    public void Empty_range_has_no_windows() =>
        Assert.Empty(SyncWindows.Split(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, TimeSpan.FromHours(24)));

    [Theory]
    [InlineData("2026-10-18T20:59:00Z", "2026-10-18T00:00:00+03:00")] // TR 23:59
    [InlineData("2026-10-18T21:00:00Z", "2026-10-19T00:00:00+03:00")] // TR 00:00 → yeni gün
    [InlineData("2026-10-18T00:30:00Z", "2026-10-18T00:00:00+03:00")] // UTC'de gün aynı, TR 03:30
    public void Quota_day_is_turkish_calendar_day(string instant, string expected) =>
        Assert.Equal(DateTimeOffset.Parse(expected), TurkeyTime.StartOfDay(DateTimeOffset.Parse(instant)));

    [Fact]
    public void Recurring_job_time_zone_resolves_and_is_utc_plus_3()
    {
        // Hangfire bu ID'yi saklar ve her hesaplamada FindSystemTimeZoneById ile çözer.
        var tz = TimeZoneInfo.FindSystemTimeZoneById(FinCopilot.Infrastructure.Jobs.SyncJobs.TimeZoneId);

        Assert.Equal(TurkeyTime.Offset, tz.GetUtcOffset(new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(TurkeyTime.Offset, tz.GetUtcOffset(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Theory]
    [InlineData("TR250900205202796328220556", "TR25 **** 0556")]
    [InlineData("TR25 0900 2052 0279 6328 2205 56", "TR25 **** 0556")]
    [InlineData("1234", "****")]
    [InlineData(null, null)]
    [InlineData(" ", null)]
    public void Iban_masking(string? iban, string? expected) => Assert.Equal(expected, Masking.Iban(iban));
}
