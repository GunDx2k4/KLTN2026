using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

internal static class ReputationModelConfiguration
{
    public static void ConfigureUser(EntityTypeBuilder<AppUser> user)
    {
        user.Property(x => x.ReputationBaselineScore).HasColumnName("reputation_baseline_score").HasPrecision(5, 2);
        user.Property(x => x.ReputationVersion).HasColumnName("reputation_version").IsConcurrencyToken().HasDefaultValue(0L);
        user.Property(x => x.ReputationComputedAt).HasColumnName("reputation_computed_at");
        user.Property(x => x.ReputationPolicyVersion).HasColumnName("reputation_policy_version").HasMaxLength(40);
        user.ToTable("app_user", t => t.HasCheckConstraint("ck_user_reputation_range",
            "reputation_score BETWEEN 0 AND 100 AND (reputation_baseline_score IS NULL OR reputation_baseline_score BETWEEN 0 AND 100)"));

    }

    public static void ConfigureGroup(EntityTypeBuilder<ClubGroup> group)
    {
        group.Property(x => x.QualityVersion).HasColumnName("quality_version").IsConcurrencyToken().HasDefaultValue(0L);
        group.Property(x => x.QualityComputedAt).HasColumnName("quality_computed_at");
        group.Property(x => x.QualityPolicyVersion).HasColumnName("quality_policy_version").HasMaxLength(40);
        Json(group.Property(x => x.QualitySummary), "quality_summary");
        group.ToTable("club_group", t =>
        {
            t.HasCheckConstraint("ck_group_quality_range", "group_quality_score BETWEEN 1 AND 5");
            t.HasCheckConstraint("ck_group_quality_json", "quality_summary IS NULL OR jsonb_typeof(quality_summary) = 'object'");
        });

    }

    public static void ConfigureSession(EntityTypeBuilder<BadmintonSession> session)
    {
        session.Property(x => x.ReputationProcessingStatus).HasColumnName("reputation_processing_status")
            .HasConversion<string>().HasMaxLength(30).HasDefaultValue(ReputationProcessingStatus.Pending);
        session.Property(x => x.FinancialReconciledAt).HasColumnName("financial_reconciled_at");
        session.Property(x => x.ReputationProcessedAt).HasColumnName("reputation_processed_at");
        session.Property(x => x.ReputationPolicyVersion).HasColumnName("reputation_policy_version").HasMaxLength(40);
        session.Property(x => x.ReputationNextAttemptAt).HasColumnName("reputation_next_attempt_at");
        session.Property(x => x.ReputationPendingReason).HasColumnName("reputation_pending_reason").HasMaxLength(200);
        session.Property(x => x.ReputationVersion).HasColumnName("reputation_version").IsConcurrencyToken().HasDefaultValue(0L);
        Json(session.Property(x => x.ReputationSnapshot), "reputation_snapshot");
        session.HasAlternateKey(x => new { x.Id, x.GroupId });
        session.HasIndex(x => new { x.ReputationProcessingStatus, x.ReputationNextAttemptAt });
        session.ToTable("badminton_session", t =>
        {
            t.HasCheckConstraint("ck_session_reputation_state", "reputation_processing_status IN ('Pending','DataInsufficient','Finalized')");
            t.HasCheckConstraint("ck_session_reputation_finalized", "reputation_processing_status <> 'Finalized' OR (financial_reconciled_at IS NOT NULL AND reputation_processed_at IS NOT NULL AND reputation_policy_version IS NOT NULL AND reputation_snapshot IS NOT NULL)");
            t.HasCheckConstraint("ck_session_reputation_json", "reputation_snapshot IS NULL OR jsonb_typeof(reputation_snapshot) = 'object'");
        });

    }

    public static void ConfigureBooking(EntityTypeBuilder<SessionBooking> booking)
    {
        Json(booking.Property(x => x.CancellationHistory), "cancellation_history").HasDefaultValueSql("'[]'::jsonb");
        Json(booking.Property(x => x.ReputationEvidence), "reputation_evidence");
        Json(booking.Property(x => x.ReputationCorrections), "reputation_corrections").HasDefaultValueSql("'[]'::jsonb");
        booking.Property(x => x.ReputationRawDelta).HasColumnName("reputation_raw_delta").HasPrecision(5, 2);
        booking.Property(x => x.ReputationComputedAt).HasColumnName("reputation_computed_at");
        booking.Property(x => x.ReputationPolicyVersion).HasColumnName("reputation_policy_version").HasMaxLength(40);
        booking.Property(x => x.ReputationReviewEligible).HasColumnName("reputation_review_eligible").HasDefaultValue(false);
        booking.Property(x => x.ReputationVersion).HasColumnName("reputation_version").IsConcurrencyToken().HasDefaultValue(0L);
        booking.HasAlternateKey(x => new { x.SessionId, x.UserId });
        booking.HasOne(x => x.Session).WithMany(x => x.Bookings).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        booking.HasOne(x => x.User).WithMany(x => x.SessionBookings).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        booking.ToTable("session_booking", t =>
        {
            t.HasCheckConstraint("ck_booking_reputation_json", "jsonb_typeof(cancellation_history) = 'array' AND jsonb_typeof(reputation_corrections) = 'array' AND (reputation_evidence IS NULL OR jsonb_typeof(reputation_evidence) = 'object')");
            t.HasCheckConstraint("ck_booking_reputation_result", "(reputation_raw_delta IS NULL AND reputation_computed_at IS NULL AND reputation_policy_version IS NULL AND reputation_evidence IS NULL AND NOT reputation_review_eligible) OR (reputation_raw_delta BETWEEN -23 AND 2 AND reputation_raw_delta IS NOT NULL AND reputation_computed_at IS NOT NULL AND reputation_policy_version IS NOT NULL AND reputation_evidence IS NOT NULL)");
        });

    }

    public static void ConfigureReview(EntityTypeBuilder<GroupReview> review)
    {
        review.Property(x => x.SessionId).HasColumnName("id_session");
        review.HasIndex(x => new { x.SessionId, x.UserId }).IsUnique().HasFilter("id_session IS NOT NULL");
        review.HasOne<BadmintonSession>().WithMany().HasForeignKey(x => new { x.SessionId, x.GroupId })
            .HasPrincipalKey(x => new { x.Id, x.GroupId }).OnDelete(DeleteBehavior.Restrict);
        review.HasOne<SessionBooking>().WithMany().HasForeignKey(x => new { x.SessionId, x.UserId })
            .HasPrincipalKey(x => new { x.SessionId, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        review.HasOne(x => x.Group).WithMany(x => x.Reviews).HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        review.HasOne(x => x.User).WithMany(x => x.GroupReviews).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        review.ToTable("group_review", t => t.HasCheckConstraint("ck_group_review_session_rating",
            "id_session IS NULL OR (rating BETWEEN 1 AND 5 AND (comment IS NULL OR char_length(comment) <= 2000))"));
    }

    private static PropertyBuilder<T> Json<T>(PropertyBuilder<T> property, string column)
    {
        property.HasColumnName(column).HasColumnType("jsonb")
            .HasConversion(value => Serialize(value), value => Deserialize<T>(value));
        property.Metadata.SetValueComparer(new ValueComparer<T>(
            (left, right) => Serialize(left) == Serialize(right),
            value => Serialize(value).GetHashCode(), value => Deserialize<T>(Serialize(value))));
        return property;
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null);
    private static T Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, (JsonSerializerOptions?)null)!;
}
