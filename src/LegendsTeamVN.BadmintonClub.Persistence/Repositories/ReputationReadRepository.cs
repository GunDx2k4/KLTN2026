using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using Microsoft.EntityFrameworkCore;
namespace LegendsTeamVN.BadmintonClub.Persistence.Repositories;
public sealed class ReputationReadRepository(BadmintonDbContext db) : IReputationReadRepository
{
    public Task<bool> CanDeliverAsync(string kind, Guid sessionId, Guid bookingId, Guid userId, DateTime? expiresAt, DateTime now, CancellationToken ct)
    {
        if (kind is not ("ReviewInvitationPush" or "ReputationUpdatedPush")) return Task.FromResult(false);
        var invitation = kind == "ReviewInvitationPush";
        if (invitation && (!expiresAt.HasValue || expiresAt <= now) || !invitation && expiresAt.HasValue) return Task.FromResult(false);
        return db.SessionBookings.AsNoTracking().AnyAsync(b => b.Id == bookingId && b.SessionId == sessionId && b.UserId == userId &&
            !b.IsDeleted && b.User.IsActive && !b.User.IsDeleted && !b.Session.IsDeleted &&
            b.Session.Group.IsActive && !b.Session.Group.IsDeleted && b.ReputationComputedAt != null &&
            b.Session.ReputationProcessingStatus == ReputationProcessingStatus.Finalized &&
            (!invitation || b.ReputationReviewEligible && b.Session.FinancialReconciledAt <= now &&
                b.Session.FinancialReconciledAt!.Value.AddDays(7) == expiresAt &&
                !db.GroupReviews.IgnoreQueryFilters().Any(r => r.SessionId == sessionId && r.UserId == userId)), ct);
    }
    public async Task<IReadOnlyList<Guid>> FindDueAsync(DateTime cutover, DateTime now, int batch, IReadOnlyList<Guid> excluded, CancellationToken ct) =>
        await db.BadmintonSessions.AsNoTracking().Where(s => !s.IsDeleted && s.Group.IsActive && !s.Group.IsDeleted &&
            s.StartTime >= cutover && s.EndTime <= now && s.Status == SessionStatus.COMPLETED &&
            s.ReputationProcessingStatus != ReputationProcessingStatus.Finalized &&
            (s.ReputationNextAttemptAt == null || s.ReputationNextAttemptAt <= now) && !excluded.Contains(s.Id))
            .OrderBy(s => s.ReputationNextAttemptAt).ThenBy(s => s.EndTime).ThenBy(s => s.Id).Take(batch).Select(s => s.Id).ToListAsync(ct);
    public Task<PlayerReputationResponse?> GetPlayerAsync(Guid userId, CancellationToken ct) =>
        db.AppUsers.AsNoTracking().Where(u => u.Id == userId && u.IsActive && !u.IsDeleted)
            .Select(u => new PlayerReputationResponse(u.Id, u.ReputationScore,
                u.ReputationScore < ReputationPolicy.RiskThreshold ? "Rủi ro bùng kèo" : null,
                u.ReputationPolicyVersion, u.ReputationComputedAt,
                db.SessionBookings.IgnoreQueryFilters().Count(b => b.UserId == userId && b.ReputationComputedAt != null))).SingleOrDefaultAsync(ct);
    public Task<GroupQualityResponse?> GetQualityAsync(Guid groupId, CancellationToken ct) =>
        db.ClubGroups.AsNoTracking().Where(g => g.Id == groupId && g.IsActive && !g.IsDeleted)
            .Select(g => new GroupQualityResponse(g.Id, g.GroupQualityScore, g.QualityPolicyVersion, g.QualityComputedAt, g.QualitySummary)).SingleOrDefaultAsync(ct);
    public async Task<ReputationPageResponse<ReputationEntryResponse>> GetEntriesAsync(Guid userId, int page, int size, CancellationToken ct)
    {
        var query = db.SessionBookings.IgnoreQueryFilters().AsNoTracking().Where(b => b.UserId == userId && b.ReputationComputedAt != null);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(b => b.ReputationComputedAt).ThenBy(b => b.Id).Skip((page - 1) * size).Take(size)
            .Select(b => new ReputationEntryResponse(b.Id, b.SessionId, b.ReputationRawDelta, b.ReputationComputedAt,
                b.ReputationPolicyVersion, b.ReputationEvidence)).ToListAsync(ct);
        return new(items, page, size, total);
    }
    public async Task<ReputationPageResponse<ReviewInvitationResponse>> GetInvitationsAsync(Guid userId, DateTime now, int page, int size, CancellationToken ct)
    {
        var query = db.SessionBookings.AsNoTracking().Where(b => b.UserId == userId && !b.IsDeleted && b.ReputationReviewEligible &&
            !b.Session.IsDeleted && b.Session.Group.IsActive && !b.Session.Group.IsDeleted &&
            b.Session.ReputationProcessingStatus == ReputationProcessingStatus.Finalized &&
            b.Session.FinancialReconciledAt <= now && b.Session.FinancialReconciledAt > now.AddDays(-7));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(b => b.Session.FinancialReconciledAt).ThenBy(b => b.Id)
            .Skip((page - 1) * size).Take(size).Select(b => new { b.SessionId, b.Session.GroupId, b.Session.FinancialReconciledAt }).ToListAsync(ct);
        var ids = rows.Select(r => r.SessionId).ToArray();
        var reviews = await db.GroupReviews.IgnoreQueryFilters().AsNoTracking().Where(r => r.UserId == userId && r.SessionId != null && ids.Contains(r.SessionId.Value))
            .Select(r => new SessionReviewResponse(r.Id, r.SessionId!.Value, r.Rating, r.Comment, r.CreatedAt)).ToListAsync(ct);
        return new(rows.Select(r => new ReviewInvitationResponse(r.SessionId, r.GroupId, r.FinancialReconciledAt!.Value.AddDays(7),
            reviews.SingleOrDefault(v => v.SessionId == r.SessionId))).ToArray(), page, size, total);
    }
    public Task<bool> CanAuditAsync(Guid groupId, Guid userId, CancellationToken ct) =>
        db.GroupMembers.AsNoTracking().AnyAsync(m => m.GroupId == groupId && m.UserId == userId && !m.IsDeleted &&
            m.Status == ClubMemberStatus.ACTIVE && m.User.IsActive && !m.User.IsDeleted && m.Group.IsActive && !m.Group.IsDeleted &&
            m.RoleEntity.GroupId == groupId && !m.RoleEntity.IsDeleted &&
            m.RoleEntity.RolePermissions.Any(p => p.Permission.PermissionCode == GroupPermissions.Operations.SessionUpdate), ct);
    public async Task<ReputationAuditResponse?> GetAuditAsync(Guid groupId, Guid sessionId, int page, int size, CancellationToken ct)
    {
        var session = await db.BadmintonSessions.AsNoTracking().Where(s => s.Id == sessionId && s.GroupId == groupId && !s.IsDeleted)
            .Select(s => new { s.ReputationProcessingStatus, s.ReputationPendingReason, s.ReputationSnapshot }).SingleOrDefaultAsync(ct);
        if (session is null) return null;
        var query = db.SessionBookings.IgnoreQueryFilters().AsNoTracking().Where(b => b.SessionId == sessionId && b.ReputationComputedAt != null);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(b => b.Id).Skip((page - 1) * size).Take(size)
            .Select(b => new { b.Id, b.UserId, b.ReputationRawDelta, b.ReputationEvidence }).ToListAsync(ct);
        var users = rows.Select(b => b.UserId).ToArray();
        var reviews = await db.GroupReviews.IgnoreQueryFilters().AsNoTracking().Where(r => r.SessionId == sessionId && users.Contains(r.UserId))
            .Select(r => new { r.UserId, Review = new SessionReviewResponse(r.Id, sessionId, r.Rating, r.Comment, r.CreatedAt) }).ToListAsync(ct);
        return new(sessionId, session.ReputationProcessingStatus.ToString(), session.ReputationPendingReason, session.ReputationSnapshot,
            new(rows.Select(b => new ReputationAuditBookingResponse(b.Id, b.UserId, b.ReputationRawDelta, b.ReputationEvidence,
                reviews.SingleOrDefault(r => r.UserId == b.UserId)?.Review)).ToArray(), page, size, total));
    }
}
