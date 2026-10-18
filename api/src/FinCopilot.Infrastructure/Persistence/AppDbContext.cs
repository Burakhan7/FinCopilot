using FinCopilot.Application.Abstractions;
using FinCopilot.Domain.BankData;
using FinCopilot.Domain.Consents;
using FinCopilot.Domain.Enrichment;
using FinCopilot.Domain.Identity;
using FinCopilot.Domain.Outputs;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FinCopilot.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IAppDbContext, IDataProtectionKeyContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserDevice> UserDevices => Set<UserDevice>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public DbSet<Bank> Banks => Set<Bank>();
    public DbSet<Consent> Consents => Set<Consent>();
    public DbSet<ConsentToken> ConsentTokens => Set<ConsentToken>();
    public DbSet<SyncJob> SyncJobs => Set<SyncJob>();
    public DbSet<EventInboxItem> EventInbox => Set<EventInboxItem>();

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<BalanceSnapshot> BalanceSnapshots => Set<BalanceSnapshot>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<CardStatement> CardStatements => Set<CardStatement>();
    public DbSet<CardTransaction> CardTransactions => Set<CardTransaction>();
    public DbSet<Installment> Installments => Set<Installment>();

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Merchant> Merchants => Set<Merchant>();
    public DbSet<MerchantAlias> MerchantAliases => Set<MerchantAlias>();
    public DbSet<RecurringItem> RecurringItems => Set<RecurringItem>();

    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<SavingsEvent> SavingsEvents => Set<SavingsEvent>();
    public DbSet<Goal> Goals => Set<Goal>();

    /// <summary>ASP.NET Data Protection anahtar halkası (Api ve Worker paylaşır).</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<decimal>().HavePrecision(19, 4);
        builder.Properties<string>().HaveMaxLength(256);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.Entity<DataProtectionKey>().Property(k => k.Xml).HasColumnType("nvarchar(max)");

        // En sonda: tablo, sütun, anahtar ve indeks adlarını snake_case yap.
        modelBuilder.ApplySnakeCaseNames();
    }
}
