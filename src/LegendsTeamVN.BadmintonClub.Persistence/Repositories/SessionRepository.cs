using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Persistence.Repositories;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Persistence.Repositories;

public class SessionRepository(BadmintonDbContext dbContext) 
    : GenericRepository<BadmintonDbContext, BadmintonSession, Guid>(dbContext), ISessionRepository
{
    public async Task<IReadOnlyList<Guid>> FindDueRotationIdsAsync(DateTime now, int limit,
        IReadOnlyCollection<Guid> excluded, CancellationToken ct) => await DbContext.BadmintonSessions.AsNoTracking()
        .Where(s => !s.IsDeleted && s.RotationStartedAt != null && s.StartTime <= now && s.EndTime > now &&
            (s.Status == SessionStatus.OPEN || s.Status == SessionStatus.LOCKED || s.Status == SessionStatus.PLAYING) &&
            s.NextProposalCheckAt != null && s.NextProposalCheckAt <= now && !excluded.Contains(s.Id))
        .OrderBy(s => s.NextProposalCheckAt).ThenBy(s => s.Id).Take(limit).Select(s => s.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindRotationRecoveryIdsAsync(DateTime now, Guid? after, int limit, CancellationToken ct) =>
        await DbContext.BadmintonSessions.AsNoTracking().Where(s => !s.IsDeleted && s.RotationStartedAt != null &&
            s.StartTime <= now && s.EndTime > now &&
            (s.Status == SessionStatus.OPEN || s.Status == SessionStatus.LOCKED || s.Status == SessionStatus.PLAYING) &&
            (after == null || s.Id.CompareTo(after.Value) > 0))
        .OrderBy(s => s.Id).Take(limit).Select(s => s.Id).ToListAsync(ct);

    // Domain assigns GUIDs itself: explicitly mark new proposals/players Added instead of EF treating them as updates.
    public void AddRotationMatches(IEnumerable<SessionMatch> matches)
    {
        foreach (var match in matches)
        {
            DbContext.Entry(match).State = EntityState.Added;
            foreach (var player in match.Players) DbContext.Entry(player).State = EntityState.Added;
        }
    }

    public Task<bool> CanReceiveMatchInvitationAsync(Guid sessionId, Guid matchId, Guid userId, CancellationToken ct) =>
        DbContext.SessionMatches.AsNoTracking().AnyAsync(m => m.Id == matchId && m.SessionId == sessionId &&
            !m.IsDeleted && !m.Session.IsDeleted && m.Status == MatchStatus.PLAYING &&
            m.Players.Any(p => p.UserId == userId && p.User.IsActive && !p.User.IsDeleted), ct);
    public async Task<BadmintonSession?> GetAttendanceAsync(Guid sessionId, bool forUpdate, CancellationToken cancellationToken = default)
    {
        if (forUpdate && DbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Attendance writes require an active transaction.");

        // Lock the parent before reading children. Every attendance writer uses this same lock.
        var query = forUpdate
            ? DbContext.BadmintonSessions.FromSqlInterpolated($"SELECT * FROM badminton_session WHERE id_session = {sessionId} FOR UPDATE")
            : DbContext.BadmintonSessions.AsQueryable();
        var session = await query.Where(s => s.Id == sessionId && !s.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
        if (session is null) return null;
        await DbContext.Entry(session).ReloadAsync(cancellationToken);

        await DbContext.Entry(session).Collection(s => s.Bookings).Query()
            .Include(b => b.User).LoadAsync(cancellationToken);
        await DbContext.Entry(session).Collection(s => s.Matches).Query()
            .IgnoreQueryFilters().Include(m => m.Players).LoadAsync(cancellationToken);
        await DbContext.Entry(session).Collection(s => s.Recruitments).Query()
            .Where(r => !r.IsDeleted).LoadAsync(cancellationToken);
        return session;
    }

    public async Task<BadmintonSession?> GetRotationAsync(Guid sessionId, bool forUpdate, CancellationToken cancellationToken = default)
    {
        if (forUpdate) return await GetAttendanceAsync(sessionId, true, cancellationToken);
        // One MVCC snapshot covers parent, bookings, history and assignments; GET never generates proposals.
        await using var snapshot = await DbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
        var session = await GetAttendanceAsync(sessionId, false, cancellationToken);
        await snapshot.CommitAsync(cancellationToken);
        return session;
    }

    public Task<bool> CanManageRotationAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default) =>
        DbContext.GroupMembers.AsNoTracking().AnyAsync(m =>
            m.GroupId == groupId && m.UserId == userId && m.Status == ClubMemberStatus.ACTIVE && !m.IsDeleted &&
            m.Group.IsActive && !m.Group.IsDeleted && m.User.IsActive && !m.User.IsDeleted &&
            m.RoleEntity.GroupId == groupId && !m.RoleEntity.IsDeleted &&
            m.RoleEntity.RolePermissions.Any(rp => rp.Permission.PermissionCode == GroupPermissions.Operations.MatchManage), cancellationToken);

    public Task<bool> CanManageAttendanceAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default) =>
        DbContext.GroupMembers.AsNoTracking().AnyAsync(m =>
            m.GroupId == groupId && m.UserId == userId && m.Status == ClubMemberStatus.ACTIVE && !m.IsDeleted &&
            m.Group.IsActive && !m.Group.IsDeleted && m.User.IsActive && !m.User.IsDeleted &&
            m.RoleEntity.GroupId == groupId && !m.RoleEntity.IsDeleted &&
            (m.RoleEntity.RoleName == "Host" || m.RoleEntity.RolePermissions.Any(rp =>
                rp.Permission.PermissionCode == GroupPermissions.Operations.BookingManage)), cancellationToken);
}
