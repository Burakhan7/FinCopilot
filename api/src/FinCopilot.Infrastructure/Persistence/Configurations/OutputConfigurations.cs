using FinCopilot.Domain.Identity;
using FinCopilot.Domain.Outputs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinCopilot.Infrastructure.Persistence.Configurations;

internal sealed class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> b)
    {
        b.ToTable(t => t.HasCheckConstraint("ck_alerts_payload_json", "ISJSON([payload]) = 1"));
        b.Property(x => x.Type).HasMaxLength(64);
        b.Property(x => x.Severity).HasMaxLength(16);
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.Payload).HasColumnType("nvarchar(max)");
        b.Property(x => x.DedupeKey).HasMaxLength(200);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.UserId, x.DedupeKey }).IsUnique();
    }
}

internal sealed class SavingsEventConfiguration : IEntityTypeConfiguration<SavingsEvent>
{
    public void Configure(EntityTypeBuilder<SavingsEvent> b)
    {
        b.ToTable(t => t.HasCheckConstraint("ck_savings_events_evidence_json", "[evidence] IS NULL OR ISJSON([evidence]) = 1"));
        b.Property(x => x.Type).HasMaxLength(32);
        b.Property(x => x.Evidence).HasColumnType("nvarchar(max)");
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Alert>().WithMany().HasForeignKey(x => x.AlertId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> b)
    {
        b.Property(x => x.Name).HasMaxLength(128);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
