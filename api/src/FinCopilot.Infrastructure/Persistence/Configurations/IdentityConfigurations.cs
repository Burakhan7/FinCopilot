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

internal sealed class OtpChallengeConfiguration : IEntityTypeConfiguration<OtpChallenge>
{
    public void Configure(EntityTypeBuilder<OtpChallenge> b)
    {
        b.Property(x => x.Phone).HasMaxLength(20);
        b.Property(x => x.CodeHash).HasMaxLength(64);
        b.HasIndex(x => new { x.Phone, x.CreatedAt });
    }
}

internal sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> b)
    {
        b.Property(x => x.RefreshTokenHash).HasMaxLength(64);
        b.HasIndex(x => x.RefreshTokenHash).IsUnique();
        b.Property(x => x.PreviousRefreshTokenHash).HasMaxLength(64);
        b.HasIndex(x => x.PreviousRefreshTokenHash);
        b.Property(x => x.RevokeReason).HasMaxLength(32);

        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        // users → user_devices zaten cascade; ikinci yol olmasın diye NoAction.
        b.HasOne<UserDevice>().WithMany().HasForeignKey(x => x.UserDeviceId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.UserDeviceId, x.RevokedAt });
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
