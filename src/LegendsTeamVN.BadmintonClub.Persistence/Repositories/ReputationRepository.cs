using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Exceptions;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace LegendsTeamVN.BadmintonClub.Persistence.Repositories;
public sealed class ReputationRepository(BadmintonDbContext db) : IReputationRepository
{
    public async Task<ReputationAssessmentSource?> LockSessionAsync(Guid sessionId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("UC20 yêu cầu transaction.");
        var groupId = await db.BadmintonSessions.AsNoTracking().Where(s => s.Id == sessionId && !s.IsDeleted)
            .Select(s => (Guid?)s.GroupId).SingleOrDefaultAsync(ct);
        if (groupId is null) return null;
        await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '3s'", ct);
        var group = await Locked(() => db.ClubGroups.FromSqlInterpolated(
            $"SELECT * FROM club_group WHERE id_group = {groupId.Value} FOR NO KEY UPDATE")
            .AsTracking().SingleOrDefaultAsync(ct));
        if (group is null || group.IsDeleted || !group.IsActive) return null;
        var session = await Locked(() => db.BadmintonSessions.FromSqlInterpolated(
            $"SELECT * FROM badminton_session WHERE id_session = {sessionId} FOR NO KEY UPDATE")
            .AsTracking().SingleOrDefaultAsync(ct));
        if (session is null || session.IsDeleted || session.GroupId != group.Id) return null;
        await db.Entry(session).Collection(s => s.Bookings).Query().IgnoreQueryFilters().LoadAsync(ct);
        await db.Entry(session).Collection(s => s.Matches).Query().IgnoreQueryFilters().LoadAsync(ct);
        var fees = await db.SessionFees.IgnoreQueryFilters().AsNoTracking().Where(f => f.SessionId == sessionId).ToListAsync(ct);
        var ids = fees.Select(f => f.Id).ToArray();
        var ledger = await db.GroupLedgers.IgnoreQueryFilters().AsNoTracking()
            .Where(l => l.SessionId == sessionId || l.FeeId != null && ids.Contains(l.FeeId.Value)).ToListAsync(ct);
        var bankIds = ledger.Where(l => l.BankTransactionId.HasValue).Select(l => l.BankTransactionId!.Value).ToArray();
        var banks = await db.BankTransactions.IgnoreQueryFilters().AsNoTracking().Where(b => bankIds.Contains(b.Id)).ToListAsync(ct);
        var expenses = ledger.Where(l => !l.IsDeleted && l.SessionId == sessionId && l.GroupId == group.Id &&
            l.TransactionType == TransactionType.OUTFLOW && l.Amount > 0).Select(l => l.Id).ToArray();
        var invoices = await db.LedgerAttachments.Where(a => expenses.Contains(a.LedgerId))
            .Select(a => a.LedgerId).Distinct().CountAsync(ct);
        var members = await db.GroupMembers.AsNoTracking().Where(m => m.GroupId == group.Id && !m.IsDeleted &&
            m.Status == ClubMemberStatus.ACTIVE && (m.MemberType == "MONTHLY" || m.MemberType == "DAILY") &&
            m.User.IsActive && !m.User.IsDeleted)
            .Select(m => m.RoleEntity.GroupId == group.Id && !m.RoleEntity.IsDeleted &&
                m.RoleEntity.RolePermissions.Any(p => p.Permission.PermissionCode == GroupPermissions.Finance.LedgerView)).ToListAsync(ct);
        var players = session.Bookings.Count(b => !b.IsDeleted && b.Status == BookingStatus.CHECKED_IN);
        var courts = session.Matches.Where(m => !m.IsDeleted && m.Status == MatchStatus.FINISHED &&
            m.TimeStart.HasValue && m.TimeEnd.HasValue && m.TimeEnd > m.TimeStart && m.CourtNumber > 0)
            .Select(m => m.CourtNumber).Distinct().Count();
        return new(session, group, fees, ledger, banks, new(invoices, expenses.Length, members.Count(x => x), members.Count, players, courts));
    }

    public async Task<IReadOnlyList<AppUser>> LockUsersAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var keys = ids.Distinct().ToArray();
        var users = await Locked(() => db.AppUsers.FromSqlInterpolated(
            $"SELECT * FROM app_user WHERE id_user = ANY({keys}) ORDER BY id_user FOR NO KEY UPDATE")
            .IgnoreQueryFilters().AsTracking().ToListAsync(ct));
        if (users.Count != keys.Length) throw new InvalidOperationException("Thiếu người chơi trong nguồn uy tín.");
        return users;
    }
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ReputationScoreEntry>>> GetHistoriesAsync(IReadOnlyList<Guid> userIds, CancellationToken ct)
    {
        var stored = await db.SessionBookings.IgnoreQueryFilters().AsNoTracking()
            .Where(b => userIds.Contains(b.UserId) && b.ReputationComputedAt != null)
            .Select(b => new { b.Id, b.UserId, b.SessionId, b.ReputationRawDelta, Snapshot = b.Session.ReputationSnapshot }).ToListAsync(ct);
        var entries = stored.ToDictionary(x => x.Id, x => (x.UserId, Entry: new ReputationScoreEntry(x.SessionId,
            x.Snapshot!.EndTime, x.ReputationRawDelta!.Value)));
        foreach (var booking in db.SessionBookings.Local.Where(b => userIds.Contains(b.UserId) && b.ReputationComputedAt.HasValue))
        {
            var snapshot = db.BadmintonSessions.Local.Single(s => s.Id == booking.SessionId).ReputationSnapshot!;
            entries[booking.Id] = (booking.UserId, new(booking.SessionId, snapshot.EndTime, booking.ReputationRawDelta!.Value));
        }
        return userIds.ToDictionary(id => id, id => (IReadOnlyList<ReputationScoreEntry>)entries.Values
            .Where(x => x.UserId == id).Select(x => x.Entry).ToArray());
    }
    public async Task<(IReadOnlyList<int> Ratings, IReadOnlyList<QualitySessionEvidence> Sessions)> GetQualitySourcesAsync(Guid groupId, CancellationToken ct)
    {
        var sessions = await db.BadmintonSessions.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.GroupId == groupId && s.ReputationSnapshot != null)
            .Select(s => new { s.Id, s.ReputationSnapshot }).ToListAsync(ct);
        var snapshots = sessions.ToDictionary(s => s.Id, s => s.ReputationSnapshot!.Quality);
        foreach (var session in db.BadmintonSessions.Local.Where(s => s.GroupId == groupId && s.ReputationSnapshot != null))
            snapshots[session.Id] = session.ReputationSnapshot!.Quality;
        var reviews = await db.GroupReviews.IgnoreQueryFilters().AsNoTracking().Where(r => r.GroupId == groupId && r.SessionId != null)
            .Select(r => new { r.Id, r.Rating }).ToListAsync(ct);
        var ratings = reviews.ToDictionary(r => r.Id, r => r.Rating);
        foreach (var review in db.GroupReviews.Local.Where(r => r.GroupId == groupId && r.SessionId != null)) ratings[review.Id] = review.Rating;
        return (ratings.Values.ToArray(), snapshots.Values.ToArray());
    }
    public Task<GroupReview?> FindReviewAsync(Guid sessionId, Guid userId, CancellationToken ct) =>
        db.GroupReviews.IgnoreQueryFilters().SingleOrDefaultAsync(r => r.SessionId == sessionId && r.UserId == userId, ct);
    public Task<BadmintonSession?> GetFinalizedAsync(Guid sessionId, CancellationToken ct) =>
        Task.FromResult(db.BadmintonSessions.Local.SingleOrDefault(s => s.Id == sessionId && s.ReputationProcessingStatus == ReputationProcessingStatus.Finalized));
    public Task<bool> IsActiveUserAsync(Guid userId, CancellationToken ct) =>
        db.AppUsers.AnyAsync(u => u.Id == userId && !u.IsDeleted && u.IsActive, ct);
    public async Task<IReadOnlyList<Guid>> GetActiveUserIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct) =>
        await db.AppUsers.AsNoTracking().Where(u => ids.Contains(u.Id) && !u.IsDeleted && u.IsActive).Select(u => u.Id).ToListAsync(ct);
    public void Add(GroupReview review) => db.GroupReviews.Add(review);
    public void Add(Notification notification) => db.Notifications.Add(notification);
    public void Add(OutboxMessage message) => db.OutboxMessages.Add(message);
    private static async Task<T> Locked<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch (PostgresException ex) when (ex.SqlState is "55P03" or "40P01" or "40001") { throw new ReputationConflictException(ex); }
    }
}
