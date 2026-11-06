using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace LegendsTeamVN.BadmintonClub.Persistence;

internal static class ReputationWriteGuard
{
    public static void Apply(ChangeTracker tracker)
    {
        tracker.DetectChanges();
        foreach (var entry in tracker.Entries<GroupReview>())
        {
            if (entry.State == EntityState.Added) continue;
            if (entry.Property(x => x.SessionId).OriginalValue.HasValue
                && (entry.State == EntityState.Deleted || entry.Properties.Any(Changed)))
                throw new InvalidOperationException("Đánh giá buổi đã gửi không được sửa hoặc xóa.");
        }
        foreach (var entry in tracker.Entries<SessionBooking>())
        {
            if (entry.State == EntityState.Deleted && (entry.Entity.ReputationComputedAt.HasValue || entry.Entity.CancellationHistory.Count > 0))
                throw new InvalidOperationException("Không được xóa nguồn uy tín hoặc lịch sử hủy.");
            if (entry.State == EntityState.Modified && entry.Property(x => x.ReputationComputedAt).OriginalValue.HasValue)
            {
                foreach (var name in new[] { nameof(SessionBooking.ReputationRawDelta), nameof(SessionBooking.ReputationEvidence),
                    nameof(SessionBooking.ReputationComputedAt), nameof(SessionBooking.ReputationPolicyVersion),
                    nameof(SessionBooking.ReputationReviewEligible), nameof(SessionBooking.CancellationHistory) })
                    if (Changed(entry.Property(name))) throw new InvalidOperationException("Nguồn uy tín đã chốt là bất biến.");
            }
            Increment(entry, nameof(SessionBooking.ReputationVersion));
        }
        foreach (var entry in tracker.Entries<BadmintonSession>())
        {
            if (entry.State == EntityState.Deleted && entry.Entity.ReputationProcessedAt.HasValue)
                throw new InvalidOperationException("Không được xóa buổi đã chốt uy tín.");
            if (entry.State == EntityState.Modified && entry.Property(x => x.ReputationProcessedAt).OriginalValue.HasValue)
                foreach (var name in new[] { nameof(BadmintonSession.ReputationSnapshot), nameof(BadmintonSession.FinancialReconciledAt),
                    nameof(BadmintonSession.ReputationProcessedAt), nameof(BadmintonSession.ReputationPolicyVersion), nameof(BadmintonSession.ReputationProcessingStatus) })
                    if (Changed(entry.Property(name))) throw new InvalidOperationException("Đối soát uy tín đã chốt là bất biến.");
            Increment(entry, nameof(BadmintonSession.ReputationVersion));
        }
        foreach (var entry in tracker.Entries<AppUser>()) Increment(entry, nameof(AppUser.ReputationVersion));
        foreach (var entry in tracker.Entries<ClubGroup>()) Increment(entry, nameof(ClubGroup.QualityVersion));
    }

    private static bool Changed(PropertyEntry property) => property.IsModified
        && !(property.Metadata.GetValueComparer()?.Equals(property.OriginalValue, property.CurrentValue)
            ?? Equals(property.OriginalValue, property.CurrentValue));

    private static void Increment(EntityEntry entry, string name)
    {
        if (entry.State != EntityState.Modified) return;
        var version = entry.Property(name);
        version.CurrentValue = checked(Math.Max((long)version.CurrentValue!, (long)version.OriginalValue! + 1));
    }
}
