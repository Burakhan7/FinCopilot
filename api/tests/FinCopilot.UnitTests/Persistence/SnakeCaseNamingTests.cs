using FinCopilot.Infrastructure.Persistence;

namespace FinCopilot.UnitTests.Persistence;

public class SnakeCaseNamingTests
{
    [Theory]
    [InlineData("Users", "users")]
    [InlineData("EventInbox", "event_inbox")]
    [InlineData("TcknHash", "tckn_hash")]
    [InlineData("HhsKod", "hhs_kod")]
    [InlineData("PK_UserSettings", "pk_user_settings")]
    [InlineData("FK_accounts_Consents_consent_id", "fk_accounts_consents_consent_id")]
    [InlineData("IX_transactions_account_id", "ix_transactions_account_id")]
    [InlineData("Address1Line", "address1_line")]
    public void ToSnakeCase_converts(string input, string expected) =>
        Assert.Equal(expected, SnakeCaseNaming.ToSnakeCase(input));
}
