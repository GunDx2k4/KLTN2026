using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class UserDeviceConfiguration : IEntityTypeConfiguration<UserDevice>
{
    public void Configure(EntityTypeBuilder<UserDevice> builder)
    {
        builder.ToTable("user_device");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasColumnName("id_device");

        builder.Property(d => d.UserId)
            .HasColumnName("id_user")
            .IsRequired();

        builder.Property(d => d.DeviceToken)
            .HasColumnName("device_token")
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(d => d.Platform)
            .HasColumnName("platform")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired(false)
            .HasComment("Nền tảng thiết bị của thiết bị người dùng. Miền giá trị: ANDROID, IOS.");

        builder.Property(d => d.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(d => d.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()");

        builder.HasOne(d => d.User)
            .WithMany(u => u.Devices)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
