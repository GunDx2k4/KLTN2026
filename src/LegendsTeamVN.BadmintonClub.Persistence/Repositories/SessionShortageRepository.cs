using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Persistence.Repositories;

public sealed class SessionShortageRepository(BadmintonDbContext db) : ISessionShortageRepository
{
    public async Task<IReadOnlyList<Guid>> FindDueAsync(DateTime now, int limit, CancellationToken ct) =>
        await db.BadmintonSessions.AsNoTracking()
            .Where(s => !s.IsDeleted && s.Group.IsActive && !s.Group.IsDeleted &&
                (s.Status == SessionStatus.OPEN || s.Status == SessionStatus.LOCKED) &&
                s.NextShortageCheckAt != null && s.NextShortageCheckAt <= now)
            .OrderBy(s => s.NextShortageCheckAt).ThenBy(s => s.Id).Take(limit).Select(s => s.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindHostsAsync(Guid groupId, CancellationToken ct) =>
        await db.GroupMembers.AsNoTracking()
            .Where(m => m.GroupId == groupId && !m.IsDeleted && m.Status == ClubMemberStatus.ACTIVE &&
                m.User.IsActive && !m.User.IsDeleted && m.Group.IsActive && !m.Group.IsDeleted &&
                m.RoleEntity.GroupId == groupId && !m.RoleEntity.IsDeleted &&
                m.RoleEntity.RolePermissions.Count >= GroupPermissions.TotalCount)
            .Select(m => m.UserId).Distinct().ToListAsync(ct);

    public Task<bool> CanDeliverAsync(Guid sessionId, Guid groupId, bool closed, DateTime now, CancellationToken ct)
    {
        var sessions = db.BadmintonSessions.AsNoTracking()
            .Where(s => s.Id == sessionId && s.GroupId == groupId && !s.IsDeleted);
        return closed
            ? sessions.AnyAsync(s => s.Status == SessionStatus.NOT_HELD, ct)
            : sessions.AnyAsync(s => (s.Status == SessionStatus.OPEN || s.Status == SessionStatus.LOCKED) &&
                s.StartTime > now && s.MinimumPlayers.HasValue &&
                s.Bookings.Count(b => !b.IsDeleted &&
                    (b.Status == BookingStatus.CONFIRMED || b.Status == BookingStatus.CHECKED_IN)) < s.MinimumPlayers.Value, ct);
    }

    public Task<bool> HasFeesAsync(Guid sessionId, CancellationToken ct) =>
        db.SessionFees.AnyAsync(f => f.SessionId == sessionId && !f.IsDeleted, ct);
}
