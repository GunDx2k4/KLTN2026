using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class SessionBookingConfiguration : IEntityTypeConfiguration<SessionBooking>
{
    public void Configure(EntityTypeBuilder<SessionBooking> builder)
    {
        builder.ToTable("session_booking");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id)
            .HasColumnName("id_booking");

        builder.Property(b => b.SessionId)
            .HasColumnName("id_session")
            .IsRequired();

        builder.Property(b => b.UserId)
            .HasColumnName("id_user")
            .IsRequired();

        builder.Property(b => b.RecruitmentId)
            .HasColumnName("id_recruitment");

        builder.Property(b => b.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue(Domain.Enums.BookingStatus.CONFIRMED)
            .HasSentinel((Domain.Enums.BookingStatus)0)
            .HasComment("Trạng thái đăng ký của biểu quyết giữ chỗ. Miền giá trị: CONFIRMED, WAITING, ABSENT, CHECKED_IN.");

        builder.Property(b => b.QueueOrder)
            .HasColumnName("queue_order")
            .HasDefaultValue(0);

        builder.Property(b => b.BookedAt)
            .HasColumnName("booked_at")
            .HasDefaultValueSql("now()");

        builder.Property(b => b.CheckinAt)
            .HasColumnName("checkin_at");

        builder.HasIndex(b => new { b.SessionId, b.UserId })
            .IsUnique();

        builder.HasOne(b => b.Session)
            .WithMany(s => s.Bookings)
            .HasForeignKey(b => b.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.User)
            .WithMany(u => u.SessionBookings)
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Recruitment)
            .WithMany(r => r.Bookings)
            .HasForeignKey(b => b.RecruitmentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
