using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class SessionBookingConfiguration : IEntityTypeConfiguration<SessionBooking>
{
    public void Configure(EntityTypeBuilder<SessionBooking> builder)
    {
        builder.ToTable("session_booking", table => table.HasCheckConstraint(
            "ck_session_booking_pause_source", "rotation_pause_source IS NULL OR rotation_pause_source IN ('SELF', 'HOST')"));

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

        builder.Property(b => b.IsRotationPaused).HasColumnName("is_rotation_paused").HasDefaultValue(false);
        builder.Property(b => b.RotationPauseSource).HasColumnName("rotation_pause_source").HasMaxLength(10)
            .HasConversion(new ValueConverter<RotationPauseSource, string>(
                value => value.ToString().ToUpperInvariant(), value => Enum.Parse<RotationPauseSource>(value, true)));
        builder.Property(b => b.SelfPauseTargetMatchId).HasColumnName("self_pause_target_match_id");
        builder.HasOne<SessionMatch>().WithMany().HasForeignKey(b => b.SelfPauseTargetMatchId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(b => b.SelfPauseTargetMatchId).HasFilter("self_pause_target_match_id IS NOT NULL");

        builder.Property(b => b.QueueOrder)
            .HasColumnName("queue_order")
            .HasDefaultValue(0);

        builder.Property(b => b.BookedAt)
            .HasColumnName("booked_at")
            .HasDefaultValueSql("now()");

        builder.Property(b => b.CheckinAt)
            .HasColumnName("checkin_at");

        builder.Property(b => b.AttendanceMarkedAt).HasColumnName("attendance_marked_at");
        builder.Property(b => b.AttendanceMarkedBy).HasColumnName("attendance_marked_by");

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
