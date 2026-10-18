using FinCopilot.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinCopilot.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.Property(x => x.Phone).HasMaxLength(20);
        b.HasIndex(x => x.Phone).IsUnique();
        b.Property(x => x.TcknHash).HasMaxLength(64);
        b.HasIndex(x => x.TcknHash).IsUnique();
        b.Property(x => x.FullName).HasMaxLength(200);

        b.HasOne(x => x.Settings).WithOne().HasForeignKey<UserSettings>(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Devices).WithOne().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UserDeviceConfiguration : IEntityTypeConfiguration<UserDevice>
{
    public void Configure(EntityTypeBuilder<UserDevice> b)
    {
        b.Property(x => x.DeviceId).HasMaxLength(128);
        b.Property(x => x.Platform).HasMaxLength(16);
        b.Property(x => x.FcmToken).HasMaxLength(512);
        b.HasIndex(x => new { x.UserId, x.DeviceId }).IsUnique();
    }
}

internal sealed class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
{
    public void Configure(EntityTypeBuilder<UserSettings> b)
    {
        b.HasKey(x => x.UserId);
        b.Property(x => x.Timezone).HasMaxLength(64);
    }
}
