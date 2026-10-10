using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notification");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id)
            .HasColumnName("id_notification");

        builder.Property(n => n.UserId)
            .HasColumnName("id_user")
            .IsRequired();

        builder.Property(n => n.Title)
            .HasColumnName("title")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(n => n.Content)
            .HasColumnName("content")
            .IsRequired();

        builder.Property(n => n.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50)
            .HasComment("Loại thông báo của thông báo. Miền giá trị: SESSION, QUEUE, PAYMENT, MATCH, GUEST_RECRUIT.");

        builder.Property(n => n.ReferenceType)
            .HasColumnName("reference_type")
            .HasMaxLength(100);

        builder.Property(n => n.ReferenceId)
            .HasColumnName("reference_id");

        builder.Property(n => n.IsRead)
            .HasColumnName("is_read")
            .HasDefaultValue(false);

        builder.Property(n => n.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
