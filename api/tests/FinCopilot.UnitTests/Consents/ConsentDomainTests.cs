using FinCopilot.Application.Users;
using FinCopilot.Domain.Consents;

namespace FinCopilot.UnitTests.Consents;

public class ConsentDomainTests
{
    private static Consent NewConsent(string drm) => new() { HhsKod = "9001", RizaDrm = drm, DrmKodHash = "x" };

    [Theory]
    [InlineData("B", "Y")]
    [InlineData("B", "K")]
    [InlineData("B", "I")]
    [InlineData("Y", "K")]
    [InlineData("Y", "I")]
    [InlineData("K", "S")]
    [InlineData("K", "I")]
    public void Allowed_transitions(string from, string to)
    {
        var c = NewConsent(from);

        Assert.True(c.TransitionTo(to, to == "I" ? "03" : null, DateTimeOffset.UtcNow));
        Assert.Equal(to, c.RizaDrm);
    }

    [Theory]
    [InlineData("K", "B")]
    [InlineData("K", "Y")]
    [InlineData("I", "K")]
    [InlineData("S", "K")]
    [InlineData("I", "S")]
    [InlineData("Y", "S")]
    public void Forbidden_transitions_throw(string from, string to) =>
        Assert.Throws<InvalidOperationException>(() => NewConsent(from).TransitionTo(to, null, DateTimeOffset.UtcNow));

    [Fact]
    public void Same_state_is_a_no_op()
    {
        var c = NewConsent("K");

        Assert.False(c.TransitionTo("K", null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Cancelling_records_reason_and_clears_drm_kod()
    {
        var c = NewConsent("B");

        c.TransitionTo("I", "13", DateTimeOffset.UtcNow);

        Assert.Equal("13", c.RizaIptDtyKod);
        Assert.Null(c.DrmKodHash);
        Assert.False(c.IsActive);
    }

    [Theory]
    [InlineData("10000000146", true)]
    [InlineData("10000000214", true)]
    [InlineData("10000000382", true)]
    [InlineData("10000000147", false)] // son hane yanlış
    [InlineData("10000000156", false)] // 10. hane yanlış
    [InlineData("01000000146", false)] // 0 ile başlıyor
    [InlineData("1000000014", false)]
    [InlineData("1000000014a", false)]
    public void Tckn_checksum(string tckn, bool valid) => Assert.Equal(valid, Tckn.IsValid(tckn));
}
