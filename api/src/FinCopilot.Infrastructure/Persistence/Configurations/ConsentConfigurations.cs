using FinCopilot.Domain.Consents;
using FinCopilot.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinCopilot.Infrastructure.Persistence.Configurations;

internal sealed class BankConfiguration : IEntityTypeConfiguration<Bank>
{
    public void Configure(EntityTypeBuilder<Bank> b)
    {
        b.HasKey(x => x.HhsKod);
        b.Property(x => x.HhsKod).HasMaxLength(8);
        b.Property(x => x.Name).HasMaxLength(128);
        b.Property(x => x.LogoUrl).HasMaxLength(512);
        b.Property(x => x.ApiBaseUrl).HasMaxLength(512);
    }
}

internal sealed class ConsentConfiguration : IEntityTypeConfiguration<Consent>
{
    public void Configure(EntityTypeBuilder<Consent> b)
    {
        b.Property(x => x.HhsKod).HasMaxLength(8);
        b.Property(x => x.RizaNo).HasMaxLength(64);
        b.HasIndex(x => x.RizaNo).IsUnique();
        b.Property(x => x.RizaDrm).HasMaxLength(1);
        b.Property(x => x.RizaIptDtyKod).HasMaxLength(2);
        b.Property(x => x.IzinTurleri).HasColumnType("nvarchar(max)");
        b.Property(x => x.GkdYontem).HasMaxLength(1);
        b.Property(x => x.DrmKodHash).HasMaxLength(64);

        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Bank>().WithMany().HasForeignKey(x => x.HhsKod).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Token).WithOne().HasForeignKey<ConsentToken>(t => t.ConsentId).OnDelete(DeleteBehavior.Cascade);

        // ÖHVPS: bir kullanıcının bir bankada tek aktif (B/Y/K) rızası olur.
        b.HasIndex(x => new { x.UserId, x.HhsKod })
            .IsUnique()
            .HasFilter("[riza_drm] IN ('B','Y','K')")
            .HasDatabaseName("ux_consents_user_bank_active");
        b.HasIndex(x => x.ErisimIzniSonTrh);

        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_consents_riza_drm", "[riza_drm] IN ('B','Y','K','S','I')");
            t.HasCheckConstraint("ck_consents_izin_turleri_json", "ISJSON([izin_turleri]) = 1");
        });
    }
}

internal sealed class ConsentTokenConfiguration : IEntityTypeConfiguration<ConsentToken>
{
    public void Configure(EntityTypeBuilder<ConsentToken> b) => b.HasKey(x => x.ConsentId);
}

internal sealed class SyncJobConfiguration : IEntityTypeConfiguration<SyncJob>
{
    public void Configure(EntityTypeBuilder<SyncJob> b)
    {
        b.Property(x => x.Resource).HasMaxLength(32);
        b.Property(x => x.PsuInitiated).HasMaxLength(1);
        b.Property(x => x.Status).HasMaxLength(16);
        b.Property(x => x.ErrorCode).HasMaxLength(64);
        b.HasOne<Consent>().WithMany().HasForeignKey(x => x.ConsentId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ConsentId, x.StartedAt });
    }
}

internal sealed class EventInboxItemConfiguration : IEntityTypeConfiguration<EventInboxItem>
{
    public void Configure(EntityTypeBuilder<EventInboxItem> b)
    {
        b.ToTable("EventInbox", t => t.HasCheckConstraint("ck_event_inbox_payload_json", "ISJSON([payload]) = 1"));
        b.Property(x => x.OlayNo).HasMaxLength(64);
        b.HasIndex(x => x.OlayNo).IsUnique();
        b.Property(x => x.OlayTipi).HasMaxLength(64);
        b.Property(x => x.KaynakTipi).HasMaxLength(64);
        b.Property(x => x.KaynakNo).HasMaxLength(64);
        b.Property(x => x.Payload).HasColumnType("nvarchar(max)");
    }
}
