using FinCopilot.Domain.BankData;
using FinCopilot.Domain.Consents;
using FinCopilot.Domain.Enrichment;
using FinCopilot.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinCopilot.Infrastructure.Persistence.Configurations;

// Silme zinciri: users → consents → accounts/cards → hareketler (cascade).
// SQL Server çoklu cascade yolunu reddettiği için diğer FK'ler NoAction.

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> b)
    {
        b.Property(x => x.HhsKod).HasMaxLength(8);
        b.Property(x => x.HspRef).HasMaxLength(64);
        b.Property(x => x.HspNoMasked).HasMaxLength(34);
        b.Property(x => x.HspShb).HasMaxLength(64);
        b.Property(x => x.SubeAdi).HasMaxLength(128);
        b.Property(x => x.KisaAd).HasMaxLength(128);
        b.Property(x => x.UrunAdi).HasMaxLength(128);
        b.Property(x => x.ParaBirimi).HasMaxLength(3);
        b.Property(x => x.HspTur).HasMaxLength(16);
        b.Property(x => x.HspTip).HasMaxLength(32);
        b.Property(x => x.HspDrm).HasMaxLength(16);

        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Consent>().WithMany().HasForeignKey(x => x.ConsentId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ConsentId, x.HspRef }).IsUnique();
    }
}

internal sealed class BalanceSnapshotConfiguration : IEntityTypeConfiguration<BalanceSnapshot>
{
    public void Configure(EntityTypeBuilder<BalanceSnapshot> b)
    {
        b.Property(x => x.ParaBirimi).HasMaxLength(3);
        b.Property(x => x.KrdDhlGstr).HasMaxLength(1);
        b.Property(x => x.Source).HasMaxLength(8);
        b.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.AccountId, x.BkyZmn });
    }
}

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> b)
    {
        b.Property(x => x.IslNo).HasMaxLength(64);
        b.Property(x => x.RefNo).HasMaxLength(64);
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.Direction).HasMaxLength(1);
        b.Property(x => x.IslTur).HasMaxLength(64);
        b.Property(x => x.IslAmc).HasMaxLength(4);
        b.Property(x => x.Kanal).HasMaxLength(4);
        b.Property(x => x.DescriptionRaw).HasMaxLength(512);
        b.Property(x => x.OhkMsj).HasMaxLength(512);
        b.Property(x => x.CounterpartyName).HasMaxLength(200);
        b.Property(x => x.CounterpartyIbanMasked).HasMaxLength(34);
        b.Property(x => x.CategorySource).HasMaxLength(16);
        b.Property(x => x.CategoryConfidence).HasPrecision(5, 4);

        b.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<RecurringItem>().WithMany().HasForeignKey(x => x.RecurringItemId).OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.AccountId, x.IslNo }).IsUnique();
        b.HasIndex(x => new { x.AccountId, x.OccurredAt })
            .IsDescending(false, true)
            .IncludeProperties(x => new { x.Amount, x.CategoryId });
        b.HasIndex(x => new { x.CategoryId, x.OccurredAt });
    }
}

internal sealed class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> b)
    {
        b.Property(x => x.HhsKod).HasMaxLength(8);
        b.Property(x => x.KartRef).HasMaxLength(64);
        b.Property(x => x.SonNo).HasMaxLength(4);
        b.Property(x => x.KartTipi).HasMaxLength(4);
        b.Property(x => x.AltKartTipi).HasMaxLength(4);
        b.Property(x => x.KartFormu).HasMaxLength(4);
        b.Property(x => x.KartTuru).HasMaxLength(4);
        b.Property(x => x.KartStatu).HasMaxLength(4);
        b.Property(x => x.KartSema).HasMaxLength(32);
        b.Property(x => x.UrunAdi).HasMaxLength(128);

        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Consent>().WithMany().HasForeignKey(x => x.ConsentId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ConsentId, x.KartRef }).IsUnique();
    }
}

internal sealed class CardStatementConfiguration : IEntityTypeConfiguration<CardStatement>
{
    public void Configure(EntityTypeBuilder<CardStatement> b)
    {
        b.Property(x => x.Currency).HasMaxLength(3);
        b.HasOne<Card>().WithMany().HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.CardId, x.StatementDate });
    }
}

internal sealed class CardTransactionConfiguration : IEntityTypeConfiguration<CardTransaction>
{
    public void Configure(EntityTypeBuilder<CardTransaction> b)
    {
        b.ToTable(t => t.HasCheckConstraint("ck_card_transactions_fx_info_json", "[fx_info] IS NULL OR ISJSON([fx_info]) = 1"));
        b.Property(x => x.IslemNo).HasMaxLength(64);
        b.Property(x => x.Currency).HasMaxLength(3);
        b.Property(x => x.IslTuru).HasMaxLength(64);
        b.Property(x => x.MerchantRaw).HasMaxLength(256);
        b.Property(x => x.DescriptionRaw).HasMaxLength(512);
        b.Property(x => x.FxInfo).HasColumnType("nvarchar(max)");
        b.Property(x => x.CategorySource).HasMaxLength(16);
        b.Property(x => x.CategoryConfidence).HasPrecision(5, 4);

        b.HasOne<Card>().WithMany().HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<RecurringItem>().WithMany().HasForeignKey(x => x.RecurringItemId).OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.CardId, x.IslemNo }).IsUnique();
        b.HasIndex(x => new { x.CardId, x.OccurredAt }).IsDescending(false, true);
    }
}

internal sealed class InstallmentConfiguration : IEntityTypeConfiguration<Installment>
{
    public void Configure(EntityTypeBuilder<Installment> b)
    {
        b.HasOne<CardTransaction>().WithMany().HasForeignKey(x => x.CardTransactionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Card>().WithMany().HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.CardId, x.DueMonth });
    }
}
