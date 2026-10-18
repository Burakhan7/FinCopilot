using FinCopilot.Domain.BankData;
using FinCopilot.Domain.Consents;
using FinCopilot.Domain.Enrichment;
using FinCopilot.Domain.Identity;
using FinCopilot.Domain.Outputs;
using Microsoft.EntityFrameworkCore;

namespace FinCopilot.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<UserDevice> UserDevices { get; }
    DbSet<UserSettings> UserSettings { get; }

    DbSet<Bank> Banks { get; }
    DbSet<Consent> Consents { get; }
    DbSet<ConsentToken> ConsentTokens { get; }
    DbSet<SyncJob> SyncJobs { get; }
    DbSet<EventInboxItem> EventInbox { get; }

    DbSet<Account> Accounts { get; }
    DbSet<BalanceSnapshot> BalanceSnapshots { get; }
    DbSet<Transaction> Transactions { get; }
    DbSet<Card> Cards { get; }
    DbSet<CardStatement> CardStatements { get; }
    DbSet<CardTransaction> CardTransactions { get; }
    DbSet<Installment> Installments { get; }

    DbSet<Category> Categories { get; }
    DbSet<Merchant> Merchants { get; }
    DbSet<MerchantAlias> MerchantAliases { get; }
    DbSet<RecurringItem> RecurringItems { get; }

    DbSet<Alert> Alerts { get; }
    DbSet<SavingsEvent> SavingsEvents { get; }
    DbSet<Goal> Goals { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
