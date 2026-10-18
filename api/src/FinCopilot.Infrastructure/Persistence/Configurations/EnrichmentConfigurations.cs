using FinCopilot.Domain.Enrichment;
using FinCopilot.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinCopilot.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.Property(x => x.Slug).HasMaxLength(64);
        b.HasIndex(x => x.Slug).IsUnique();
        b.Property(x => x.NameTr).HasMaxLength(128);
        b.Property(x => x.Icon).HasMaxLength(64);
        b.HasOne<Category>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.NoAction);

        // docs/veritabani.md seed listesi
        b.HasData(
            new Category { Id = 1, Slug = "market", NameTr = "Market", SortOrder = 1 },
            new Category { Id = 2, Slug = "yeme-icme", NameTr = "Yeme-İçme", SortOrder = 2 },
            new Category { Id = 3, Slug = "ulasim", NameTr = "Ulaşım", SortOrder = 3 },
            new Category { Id = 4, Slug = "fatura", NameTr = "Fatura", SortOrder = 4 },
            new Category { Id = 5, Slug = "kira", NameTr = "Kira", SortOrder = 5 },
            new Category { Id = 6, Slug = "abonelik", NameTr = "Abonelik", SortOrder = 6 },
            new Category { Id = 7, Slug = "saglik", NameTr = "Sağlık", SortOrder = 7 },
            new Category { Id = 8, Slug = "egitim", NameTr = "Eğitim", SortOrder = 8 },
            new Category { Id = 9, Slug = "eglence", NameTr = "Eğlence", SortOrder = 9 },
            new Category { Id = 10, Slug = "giyim", NameTr = "Giyim", SortOrder = 10 },
            new Category { Id = 11, Slug = "maas", NameTr = "Maaş", IsIncome = true, SortOrder = 11 },
            new Category { Id = 12, Slug = "transfer", NameTr = "Transfer", SortOrder = 12 },
            new Category { Id = 13, Slug = "banka-masrafi", NameTr = "Banka Masrafı", SortOrder = 13 },
            new Category { Id = 14, Slug = "diger", NameTr = "Diğer", SortOrder = 14 });
    }
}

internal sealed class MerchantConfiguration : IEntityTypeConfiguration<Merchant>
{
    public void Configure(EntityTypeBuilder<Merchant> b)
    {
        b.Property(x => x.CanonicalName).HasMaxLength(128);
        b.HasIndex(x => x.CanonicalName).IsUnique();
        b.Property(x => x.DisplayName).HasMaxLength(128);
        b.Property(x => x.LogoUrl).HasMaxLength(512);
        b.HasOne<Category>().WithMany().HasForeignKey(x => x.DefaultCategoryId).OnDelete(DeleteBehavior.NoAction);
        b.HasMany(x => x.Aliases).WithOne().HasForeignKey(a => a.MerchantId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class MerchantAliasConfiguration : IEntityTypeConfiguration<MerchantAlias>
{
    public void Configure(EntityTypeBuilder<MerchantAlias> b)
    {
        b.Property(x => x.PatternType).HasMaxLength(16);
        b.Property(x => x.Source).HasMaxLength(16);
    }
}

internal sealed class RecurringItemConfiguration : IEntityTypeConfiguration<RecurringItem>
{
    public void Configure(EntityTypeBuilder<RecurringItem> b)
    {
        b.Property(x => x.Type).HasMaxLength(16);
        b.Property(x => x.Name).HasMaxLength(128);
        b.Property(x => x.CounterpartyKey).HasMaxLength(128);
        b.Property(x => x.Status).HasMaxLength(16);
        b.Property(x => x.Source).HasMaxLength(16);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Merchant>().WithMany().HasForeignKey(x => x.MerchantId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.UserId, x.Status });
    }
}
