using FinCopilot.Application.Abstractions;
using FinCopilot.Domain.BankData;
using FinCopilot.Domain.Consents;
using FinCopilot.Domain.Identity;
using FinCopilot.Domain.Outputs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinCopilot.IntegrationTests;

[Collection(ApiCollection.Name)]
public class SchemaTests(ApiFactory factory)
{
    private static User NewUser() => new()
    {
        Id = Guid.NewGuid(),
        Phone = "5" + Random.Shared.NextInt64(100_000_000, 999_999_999),
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static Consent NewConsent(Guid userId, string drm) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        HhsKod = ApiFactory.TestBankCode,
        RizaDrm = drm,
        IzinTurleri = ["01", "03"],
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task Categories_are_seeded()
    {
        await using var db = factory.CreateDbContext();

        Assert.Equal(14, await db.Categories.CountAsync());
        Assert.True(await db.Categories.AnyAsync(c => c.Slug == "maas" && c.IsIncome));
    }

    [Fact]
    public async Task User_cannot_have_two_active_consents_for_same_bank()
    {
        await using var db = factory.CreateDbContext();
        var user = NewUser();
        db.Users.Add(user);
        db.Consents.Add(NewConsent(user.Id, RizaDurumu.YetkiKullanildi));
        await db.SaveChangesAsync();

        db.Consents.Add(NewConsent(user.Id, RizaDurumu.YetkiBekleniyor));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Ended_consents_do_not_block_a_new_one()
    {
        await using var db = factory.CreateDbContext();
        var user = NewUser();
        db.Users.Add(user);
        db.Consents.Add(NewConsent(user.Id, RizaDurumu.YetkiSonlandirildi));
        db.Consents.Add(NewConsent(user.Id, RizaDurumu.YetkiIptal));
        db.Consents.Add(NewConsent(user.Id, RizaDurumu.YetkiBekleniyor));

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Json_columns_reject_invalid_json()
    {
        await using var db = factory.CreateDbContext();
        var user = NewUser();
        db.Users.Add(user);
        db.Alerts.Add(new Alert
        {
            UserId = user.Id, Type = "test", Severity = "info", Title = "t",
            Payload = "bu json değil", DedupeKey = "k", CreatedAt = DateTimeOffset.UtcNow,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Deleting_user_removes_all_their_bank_data()
    {
        var user = NewUser();
        var consent = NewConsent(user.Id, RizaDurumu.YetkiKullanildi);
        var account = new Account { Id = Guid.NewGuid(), UserId = user.Id, ConsentId = consent.Id, HhsKod = ApiFactory.TestBankCode, HspRef = "H1", ParaBirimi = "TRY" };
        var card = new Card { Id = Guid.NewGuid(), UserId = user.Id, ConsentId = consent.Id, HhsKod = ApiFactory.TestBankCode, KartRef = "K1", KartTipi = "K" };
        var cardTx = new CardTransaction { Id = Guid.NewGuid(), CardId = card.Id, IslemNo = "C1", Amount = 1200m, Currency = "TRY", InstallmentCount = 3, OccurredAt = DateTimeOffset.UtcNow };

        await using (var db = factory.CreateDbContext())
        {
            var device = new UserDevice { Id = Guid.NewGuid(), UserId = user.Id, DeviceId = "d1", Platform = "android" };
            db.AddRange(user, consent, account, card, cardTx, device);
            db.Add(new UserSession { UserId = user.Id, UserDeviceId = device.Id, RefreshTokenHash = Guid.NewGuid().ToString("N"), ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) });
            db.Add(new ConsentToken { ConsentId = consent.Id, AccessTokenEnc = [1, 2, 3], AccessExpiresAt = DateTimeOffset.UtcNow });
            db.Add(new Transaction { AccountId = account.Id, IslNo = "T1", Amount = 99.90m, Currency = "TRY", Direction = "B", OccurredAt = DateTimeOffset.UtcNow });
            db.Add(new BalanceSnapshot { AccountId = account.Id, BkyTtr = 1234.50m, ParaBirimi = "TRY", BkyZmn = DateTimeOffset.UtcNow, Source = "sync" });
            db.Add(new Installment { CardTransactionId = cardTx.Id, CardId = card.Id, InstallmentNo = 1, TotalInstallments = 3, Amount = 400m, DueMonth = new DateOnly(2026, 11, 1) });
            await db.SaveChangesAsync();
        }

        await using (var db = factory.CreateDbContext())
        {
            await db.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync();

            Assert.False(await db.UserSessions.AnyAsync(s => s.UserId == user.Id));
            Assert.False(await db.UserDevices.AnyAsync(d => d.UserId == user.Id));
            Assert.False(await db.Consents.AnyAsync(c => c.UserId == user.Id));
            Assert.False(await db.ConsentTokens.AnyAsync(t => t.ConsentId == consent.Id));
            Assert.False(await db.Accounts.AnyAsync(a => a.UserId == user.Id));
            Assert.False(await db.Transactions.AnyAsync(t => t.AccountId == account.Id));
            Assert.False(await db.BalanceSnapshots.AnyAsync(b => b.AccountId == account.Id));
            Assert.False(await db.Cards.AnyAsync(c => c.UserId == user.Id));
            Assert.False(await db.CardTransactions.AnyAsync(t => t.CardId == card.Id));
            Assert.False(await db.Installments.AnyAsync(i => i.CardId == card.Id));
        }
    }

    [Fact]
    public async Task Secrets_are_protected_with_keys_stored_in_database()
    {
        var protector = factory.Services.GetRequiredService<ISecretProtector>();

        var cipher = protector.Protect("erisim-belirteci", SecretPurpose.BankToken);

        Assert.Equal("erisim-belirteci", protector.Unprotect(cipher, SecretPurpose.BankToken));
        await using var db = factory.CreateDbContext();
        Assert.True(await db.DataProtectionKeys.AnyAsync());
    }
}
