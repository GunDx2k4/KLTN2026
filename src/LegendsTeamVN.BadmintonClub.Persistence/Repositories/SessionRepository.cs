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
        if (forUpdate) await DbContext.Entry(session).ReloadAsync(cancellationToken);

        await DbContext.Entry(session).Collection(s => s.Bookings).Query()
            .Include(b => b.User).LoadAsync(cancellationToken);
        await DbContext.Entry(session).Collection(s => s.Matches).Query()
            .Where(m => !m.IsDeleted).Include(m => m.Players).LoadAsync(cancellationToken);
        await DbContext.Entry(session).Collection(s => s.Recruitments).Query()
            .Where(r => !r.IsDeleted).LoadAsync(cancellationToken);
        return session;
    }

    public Task<bool> CanManageAttendanceAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default) =>
        DbContext.GroupMembers.AsNoTracking().AnyAsync(m =>
            m.GroupId == groupId && m.UserId == userId && m.Status == ClubMemberStatus.ACTIVE && !m.IsDeleted &&
            m.Group.IsActive && !m.Group.IsDeleted && m.User.IsActive && !m.User.IsDeleted &&
            m.RoleEntity.GroupId == groupId && !m.RoleEntity.IsDeleted &&
            (m.RoleEntity.RoleName == "Host" || m.RoleEntity.RolePermissions.Any(rp =>
                rp.Permission.PermissionCode == GroupPermissions.Operations.BookingManage)), cancellationToken);
}
