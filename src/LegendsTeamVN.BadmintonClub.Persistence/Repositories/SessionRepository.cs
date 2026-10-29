using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Domain.Exceptions;
using Npgsql;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.Core.Persistence.Repositories;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Persistence.Repositories;

public class SessionRepository(BadmintonDbContext dbContext) 
    : GenericRepository<BadmintonDbContext, BadmintonSession, Guid>(dbContext), ISessionRepository
{
    private IQueryable<SessionBooking> PersonalParticipants(Guid sessionId) => DbContext.SessionBookings.AsNoTracking()
        .Where(b => b.SessionId == sessionId && !b.IsDeleted && b.Status == BookingStatus.CHECKED_IN &&
            !b.Session.IsDeleted && b.User.IsActive && !b.User.IsDeleted && b.Session.Group.IsActive && !b.Session.Group.IsDeleted &&
            !DbContext.GroupMembers.Any(m => m.GroupId == b.Session.GroupId && m.UserId == b.UserId && !m.IsDeleted && m.Status == ClubMemberStatus.BANNED));

    public async Task<IReadOnlyList<Guid>> FindPersonalScheduleRecipientsAsync(Guid sessionId, CancellationToken ct) =>
        await PersonalParticipants(sessionId).Select(b => b.UserId).ToListAsync(ct);

    public Task<bool> CanAccessPersonalScheduleAsync(Guid sessionId, Guid userId, CancellationToken ct) =>
        PersonalParticipants(sessionId).AnyAsync(b => b.UserId == userId, ct);

    public async Task<Domain.Services.MatchInvitationRoster?> GetMatchInvitationRosterAsync(Guid sessionId, Guid matchId,
        bool requirePlaying, CancellationToken ct)
    {
        // Before SaveChanges the start lives in the tracked aggregate; delivery only reads committed PLAYING rows.
        var match = requirePlaying ? null : DbContext.SessionMatches.Local.FirstOrDefault(m => m.Id == matchId && m.SessionId == sessionId && !m.IsDeleted);
        match ??= await DbContext.SessionMatches.AsNoTracking().Include(m => m.Players).SingleOrDefaultAsync(m =>
            m.Id == matchId && m.SessionId == sessionId && !m.IsDeleted && !m.Session.IsDeleted &&
            (!requirePlaying || m.Status == MatchStatus.PLAYING), ct);
        if (match is null) return null;
        var ids = match.Players.Select(p => p.UserId).ToArray();
        var names = await DbContext.AppUsers.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        foreach (var user in DbContext.AppUsers.Local.Where(u => ids.Contains(u.Id))) names[user.Id] = user.FullName;
        return new(sessionId, matchId, match.CourtNumber, match.TimeStart,
            match.Players.Select(p => new Domain.Services.InvitationPlayer(p.UserId, names.GetValueOrDefault(p.UserId, ""), p.SideTeam)).ToArray());
    }
    public async Task<PersonalRotationSnapshot> GetPersonalRotationAsync(Guid sessionId, Guid userId,
        bool forUpdate, CancellationToken ct = default)
    {
        // Reads authorize and load all children in one MVCC snapshot; commands already own their UoW.
        await using var snapshot = forUpdate ? null :
            await DbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        if (forUpdate && DbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Personal rotation writes require an active transaction.");
        var query = forUpdate ? DbContext.BadmintonSessions.FromSqlInterpolated(
            $"SELECT * FROM badminton_session WHERE id_session = {sessionId} FOR UPDATE") : DbContext.BadmintonSessions.AsQueryable();
        var session = await query.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sessionId && !s.IsDeleted, ct);
        var allowed = session is not null && await CanAccessPersonalScheduleAsync(sessionId, userId, ct);
        // Do not materialize another participant's lineup/metadata before authorization succeeds.
        if (allowed) session = await GetAttendanceAsync(sessionId, false, ct);
        if (snapshot is not null) await snapshot.CommitAsync(ct);
        return new(session, allowed);
    }
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

    public void SynchronizeRotationPlayers(SessionMatch match, IReadOnlyCollection<MatchPlayer> previousPlayers)
    {
        var retained = match.Players.Select(p => p.Id).ToHashSet();
        var previousIds = previousPlayers.Select(p => p.Id).ToHashSet();
        foreach (var player in previousPlayers.Where(p => !retained.Contains(p.Id)))
            DbContext.Entry(player).State = EntityState.Deleted;
        foreach (var player in match.Players.Where(p => !previousIds.Contains(p.Id)))
            DbContext.Entry(player).State = EntityState.Added;
    }

    public async Task<bool> CanEditManualRotationAsync(Guid groupId, Guid userId, CancellationToken ct = default)
    {
        var role = await DbContext.GroupMembers.AsNoTracking().Where(m => m.GroupId == groupId && m.UserId == userId &&
                m.Status == ClubMemberStatus.ACTIVE && !m.IsDeleted && m.Group.IsActive && !m.Group.IsDeleted &&
                m.User.IsActive && !m.User.IsDeleted && m.RoleEntity.GroupId == groupId && !m.RoleEntity.IsDeleted &&
                m.RoleEntity.RolePermissions.Any(rp => rp.Permission.PermissionCode == GroupPermissions.Operations.MatchManage))
            .Select(m => new { m.RoleEntity.RoleName, PermissionCount = m.RoleEntity.RolePermissions.Count }).SingleOrDefaultAsync(ct);
        return role is not null && (string.Equals(role.RoleName, "Host", StringComparison.OrdinalIgnoreCase) ||
            role.PermissionCount >= GroupPermissions.TotalCount);
    }

    public async Task<bool> CanReceiveMatchInvitationAsync(Guid sessionId, Guid matchId, Guid userId, CancellationToken ct) =>
        await CanAccessPersonalScheduleAsync(sessionId, userId, ct) &&
        await DbContext.SessionMatches.AsNoTracking().AnyAsync(m => m.Id == matchId && m.SessionId == sessionId &&
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
        try
        {
            await DbContext.Entry(session).Collection(s => s.Matches).Query()
                .IgnoreQueryFilters().Include(m => m.Players).LoadAsync(cancellationToken);
        }
        catch (Exception ex) when (IsSnapshotReadFailure(ex))
        {
            throw new InvalidLineupSnapshotException(sessionId);
        }
        foreach (var match in session.Matches)
        {
            try { match.OriginalLineupSnapshot?.Validate(); }
            catch (InvalidOperationException)
            {
                throw new InvalidLineupSnapshotException(sessionId, match.Id);
            }
        }
        session.ValidateRotationPauseMetadata();
        await DbContext.Entry(session).Collection(s => s.Recruitments).Query()
            .Where(r => !r.IsDeleted).LoadAsync(cancellationToken);
        return session;
    }

    private static bool IsSnapshotReadFailure(Exception exception)
    {
        for (Exception? error = exception; error is not null; error = error.InnerException)
        {
            if (error is JsonException) return true;
            if (error is FormatException or InvalidOperationException &&
                (error.StackTrace?.Contains("System.Text.Json", StringComparison.Ordinal) == true ||
                 error.StackTrace?.Contains("Storage.Json", StringComparison.Ordinal) == true)) return true;
        }
        return false;
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

    public async Task<BadmintonSession?> GetSettlementAsync(Guid sessionId, bool forUpdate, CancellationToken cancellationToken = default)
    {
        if (forUpdate)
        {
            var tracked = await GetAttendanceAsync(sessionId, true, cancellationToken);
            if (tracked is not null)
                await DbContext.Entry(tracked).Collection(s => s.Fees).Query()
                    .Include(f => f.User).LoadAsync(cancellationToken);
            return tracked;
        }

        return await DbContext.BadmintonSessions.AsNoTracking()
            .Include(s => s.Bookings).ThenInclude(b => b.User)
            .Include(s => s.Fees).ThenInclude(f => f.User)
            .FirstOrDefaultAsync(s => s.Id == sessionId && !s.IsDeleted, cancellationToken);
    }

    public async Task<(BadmintonSession? Session, GroupFeeRule? Rule, IReadOnlyList<AppUser> Users, IReadOnlyList<GroupMember> Memberships)> GetSettlementSourceAsync(
        Guid sessionId, bool forUpdate, CancellationToken cancellationToken = default)
    {
        try
        {
            return await ReadSettlementSourceAsync(sessionId, forUpdate, cancellationToken);
        }
        catch (PostgresException ex) when (forUpdate && ex.SqlState is PostgresErrorCodes.LockNotAvailable or PostgresErrorCodes.DeadlockDetected)
        {
            throw new SettlementPublicationConflictException("Settlement.Busy", "Nguồn quyết toán đang được thay đổi, hãy thử lại.", ex);
        }
    }

    private async Task<(BadmintonSession? Session, GroupFeeRule? Rule, IReadOnlyList<AppUser> Users, IReadOnlyList<GroupMember> Memberships)> ReadSettlementSourceAsync(
        Guid sessionId, bool forUpdate, CancellationToken cancellationToken = default)
    {
        if (forUpdate && DbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Settlement writes require an active transaction.");
        if (!forUpdate && DbContext.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Settlement previews require their own repeatable-read snapshot.");
        if (forUpdate && DbContext.ChangeTracker.Entries().Any(e => e.Entity is BadmintonSession or SessionBooking or
            SessionMatch or MatchPlayer or GuestRecruitment or SessionFee or AppUser or GroupMember or GroupFeeRule))
            throw new InvalidOperationException("Settlement sources must be loaded freshly before tracking financial changes.");
        await using var snapshot = !forUpdate && DbContext.Database.CurrentTransaction is null
            ? await DbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken)
            : null;
        var groupId = await DbContext.BadmintonSessions.AsNoTracking()
            .Where(s => s.Id == sessionId && !s.IsDeleted).Select(s => (Guid?)s.GroupId).SingleOrDefaultAsync(cancellationToken);
        if (!groupId.HasValue) return (null, null, Array.Empty<AppUser>(), Array.Empty<GroupMember>());
        if (forUpdate)
        {
            await DbContext.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '3s'", cancellationToken);
            await DbContext.ClubGroups.FromSqlInterpolated($"SELECT * FROM club_group WHERE id_group = {groupId.Value} FOR UPDATE")
                .IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken);
            await DbContext.BadmintonSessions.FromSqlInterpolated($"SELECT * FROM badminton_session WHERE id_session = {sessionId} FOR UPDATE")
                .IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken);
            await DbContext.AppUsers.FromSqlInterpolated($"SELECT * FROM app_user WHERE id_user IN (SELECT id_user FROM session_booking WHERE id_session = {sessionId}) ORDER BY id_user FOR SHARE")
                .IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken);
            await DbContext.GroupMembers.FromSqlInterpolated($"SELECT * FROM group_member WHERE id_group = {groupId.Value} AND id_user IN (SELECT id_user FROM session_booking WHERE id_session = {sessionId}) ORDER BY id_member FOR SHARE")
                .IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken);
            await DbContext.GroupFeeRules.FromSqlInterpolated($"SELECT * FROM group_fee_rule WHERE id_group = {groupId.Value} ORDER BY id_fee_rule FOR SHARE")
                .IgnoreQueryFilters().AsNoTracking().ToListAsync(cancellationToken);
        }
        var session = await DbContext.BadmintonSessions.AsNoTrackingWithIdentityResolution()
            .Include(s => s.Bookings).ThenInclude(b => b.User)
            .Include(s => s.Fees).ThenInclude(f => f.User)
            .Include(s => s.Matches).ThenInclude(m => m.Players)
            .Include(s => s.Recruitments).AsSplitQuery()
            .SingleOrDefaultAsync(s => s.Id == sessionId && !s.IsDeleted, cancellationToken);
        if (session is null) return (null, null, Array.Empty<AppUser>(), Array.Empty<GroupMember>());
        if (session.GroupId != groupId.Value)
            throw new InvalidOperationException("Settlement group changed while acquiring source locks.");
        session.ValidateRotationPauseMetadata();
        var userIds = session.Bookings.Where(b => !b.IsDeleted).Select(b => b.UserId).Distinct().ToArray();
        var users = await DbContext.AppUsers.IgnoreQueryFilters().AsNoTracking().Where(u => userIds.Contains(u.Id)).ToListAsync(cancellationToken);
        var memberships = await DbContext.GroupMembers.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.GroupId == session.GroupId && userIds.Contains(m.UserId)).ToListAsync(cancellationToken);
        var rules = await DbContext.GroupFeeRules.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.GroupId == session.GroupId).ToListAsync(cancellationToken);
        var rule = rules.OrderByDescending(r => r.AppliedAt)
            .ThenByDescending(r => r.Id.ToString("N"), StringComparer.Ordinal).FirstOrDefault();
        if (forUpdate) DbContext.Attach(session);
        if (snapshot is not null) await snapshot.CommitAsync(cancellationToken);
        return (session, rule, users, memberships);
    }

    public Task<bool> CanViewFinanceAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default) =>
        DbContext.GroupMembers.AsNoTracking().AnyAsync(m =>
            m.GroupId == groupId && m.UserId == userId && m.Status == ClubMemberStatus.ACTIVE && !m.IsDeleted &&
            m.Group.IsActive && !m.Group.IsDeleted && m.User.IsActive && !m.User.IsDeleted &&
            m.RoleEntity.GroupId == groupId && !m.RoleEntity.IsDeleted &&
            m.RoleEntity.RolePermissions.Any(rp => rp.Permission.PermissionCode == GroupPermissions.Finance.FeeView), cancellationToken);
    public void AddSettlementFees(IEnumerable<SessionFee> fees)
    {
        foreach (var fee in fees)
            DbContext.Entry(fee).State = EntityState.Added;
    }

    public Task<bool> CanManageFinanceAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default) =>
        DbContext.GroupMembers.AsNoTracking().AnyAsync(m =>
            m.GroupId == groupId && m.UserId == userId && m.Status == ClubMemberStatus.ACTIVE && !m.IsDeleted &&
            m.Group.IsActive && !m.Group.IsDeleted && m.User.IsActive && !m.User.IsDeleted &&
            m.RoleEntity.GroupId == groupId && !m.RoleEntity.IsDeleted &&
            m.RoleEntity.RolePermissions.Any(rp => rp.Permission.PermissionCode == GroupPermissions.Finance.FeeManage), cancellationToken);

    public Task<bool> CanManageAttendanceAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default) =>
        DbContext.GroupMembers.AsNoTracking().AnyAsync(m =>
            m.GroupId == groupId && m.UserId == userId && m.Status == ClubMemberStatus.ACTIVE && !m.IsDeleted &&
            m.Group.IsActive && !m.Group.IsDeleted && m.User.IsActive && !m.User.IsDeleted &&
            m.RoleEntity.GroupId == groupId && !m.RoleEntity.IsDeleted &&
            (m.RoleEntity.RoleName == "Host" || m.RoleEntity.RolePermissions.Any(rp =>
                rp.Permission.PermissionCode == GroupPermissions.Operations.BookingManage)), cancellationToken);
}
