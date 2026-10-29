using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class BadmintonSessionConfiguration : IEntityTypeConfiguration<BadmintonSession>
{
    public void Configure(EntityTypeBuilder<BadmintonSession> builder)
    {
        builder.Property(s => s.NextProposalCheckAt).HasColumnName("next_proposal_check_at");
        builder.HasIndex(s => s.NextProposalCheckAt).HasFilter("next_proposal_check_at IS NOT NULL AND NOT \"IsDeleted\"");
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

        builder.Property(s => s.AttendanceCompletedAt).HasColumnName("attendance_completed_at");
        builder.Property(s => s.AttendanceCompletedBy).HasColumnName("attendance_completed_by");
        builder.Property(s => s.RotationStartedAt).HasColumnName("rotation_started_at");
        builder.Property(s => s.RotationStartedBy).HasColumnName("rotation_started_by");
        builder.Property(s => s.RotationVersion).HasColumnName("rotation_version").HasDefaultValue(0L);

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

        builder.Property(s => s.ShuttlecockPrice).HasColumnName("shuttlecock_price").HasPrecision(18, 2);
        builder.Property(s => s.PublishedDailyMemberFee).HasColumnName("published_daily_member_fee").HasPrecision(18, 2);
        builder.Property(s => s.PublishedGuestFee).HasColumnName("published_guest_fee").HasPrecision(18, 2);
        builder.Property(s => s.PublishedFemaleDiscountDaily).HasColumnName("published_female_discount_daily").HasPrecision(18, 2);
        builder.Property(s => s.PublishedFemaleDiscountGuest).HasColumnName("published_female_discount_guest").HasPrecision(18, 2);
        builder.Property(s => s.TotalCost).HasColumnName("total_cost").HasPrecision(18, 2);
        builder.Property(s => s.TotalReceivable).HasColumnName("total_receivable").HasPrecision(18, 2);
        builder.Property(s => s.MonthlyFundCovered).HasColumnName("monthly_fund_covered").HasPrecision(18, 2);
        builder.Property(s => s.TotalDiscount).HasColumnName("total_discount").HasPrecision(18, 2);
        builder.Property(s => s.PolicyDelta).HasColumnName("policy_delta").HasPrecision(18, 2);
        builder.Property(s => s.RoundingDelta).HasColumnName("rounding_delta").HasPrecision(18, 2);
        builder.Property(s => s.AdjustmentDelta).HasColumnName("adjustment_delta").HasPrecision(18, 2);
        builder.Property(s => s.BalanceDifference).HasColumnName("balance_difference").HasPrecision(18, 2);
        builder.Property(s => s.PublishedPreviewId).HasColumnName("published_preview_id");
        builder.Property(s => s.PublishedAt).HasColumnName("published_at");
        builder.Property(s => s.PublishedBy).HasColumnName("published_by");
        builder.Property(s => s.PublishedFeeRuleId).HasColumnName("published_fee_rule_id");
        builder.Property(s => s.PublishedFeeRuleAppliedAt).HasColumnName("published_fee_rule_applied_at");
        builder.Property(s => s.PublishedRoundRule).HasColumnName("published_round_rule");
        builder.Property(s => s.PublishedRequestHash).HasColumnName("published_request_hash").HasMaxLength(64);
        builder.Property(s => s.PublishedFeeMode).HasColumnName("published_fee_mode").HasConversion<string>().HasMaxLength(50);

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
