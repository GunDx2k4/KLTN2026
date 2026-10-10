using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class BadmintonSessionConfiguration : IEntityTypeConfiguration<BadmintonSession>
{
    public void Configure(EntityTypeBuilder<BadmintonSession> builder)
    {
        builder.ToTable("badminton_session");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasColumnName("id_session");

        builder.Property(s => s.GroupId)
            .HasColumnName("id_group")
            .IsRequired();

        builder.Property(s => s.Title)
            .HasColumnName("title")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(s => s.StartTime)
            .HasColumnName("start_time")
            .IsRequired();

        builder.Property(s => s.EndTime)
            .HasColumnName("end_time")
            .IsRequired();

        builder.Property(s => s.CourtLocation)
            .HasColumnName("court_location")
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(s => s.CourtCount)
            .HasColumnName("court_count")
            .IsRequired();

        builder.Property(s => s.TargetPlayerPerCourt)
            .HasColumnName("target_player_per_court")
            .HasDefaultValue(6);

        builder.Property(s => s.MaxPlayers)
            .HasColumnName("max_players")
            .IsRequired();

        builder.Property(s => s.DeadlineBooking)
            .HasColumnName("deadline_booking")
            .IsRequired();

        builder.Property(s => s.ShuttlecockUsed)
            .HasColumnName("shuttlecock_used")
            .HasDefaultValue(0);

        builder.Property(s => s.CourtRentalCost)
            .HasColumnName("court_rental_cost")
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(s => s.ExtraExpense)
            .HasColumnName("extra_expense")
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue(Domain.Enums.SessionStatus.OPEN)
            .HasSentinel((Domain.Enums.SessionStatus)0)
            .HasComment("Trạng thái buổi chơi của buổi sinh hoạt. Miền giá trị: OPEN, LOCKED, PLAYING, CALCULATED, COMPLETED.");

        builder.Ignore(s => s.FeePerMember);

        builder.HasOne(s => s.Group)
            .WithMany(g => g.Sessions)
            .HasForeignKey(s => s.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Bookings)
            .WithOne(b => b.Session)
            .HasForeignKey(b => b.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Recruitments)
            .WithOne(r => r.Session)
            .HasForeignKey(r => r.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Matches)
            .WithOne(m => m.Session)
            .HasForeignKey(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Fees)
            .WithOne(f => f.Session)
            .HasForeignKey(f => f.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.LedgerTransactions)
            .WithOne(l => l.Session)
            .HasForeignKey(l => l.SessionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
