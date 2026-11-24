using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Persistence.Repositories;

public sealed class NotificationRepository : GenericRepository<BadmintonDbContext, Notification, Guid>, INotificationRepository
{
    private readonly BadmintonDbContext _dbContext;

    public NotificationRepository(BadmintonDbContext dbContext) : base(dbContext) => _dbContext = dbContext;

    public async Task<Guid?> ResolveLedgerGroupIdAsync(Guid ledgerId, Guid recipientId, CancellationToken ct)
    {
        var groupId = await _dbContext.GroupLedgers.AsNoTracking()
            .Where(l => l.Id == ledgerId && !l.IsDeleted)
            .Select(l => (Guid?)l.GroupId)
            .SingleOrDefaultAsync(ct);
        if (!groupId.HasValue) return null;
        var allowed = await _dbContext.GroupMembers.AsNoTracking().AnyAsync(m =>
            m.GroupId == groupId.Value && m.UserId == recipientId &&
            m.Status == ClubMemberStatus.ACTIVE && !m.IsDeleted &&
            (m.MemberType == "MONTHLY" || m.MemberType == "DAILY") &&
            m.Group.IsActive && !m.Group.IsDeleted &&
            m.User.IsActive && !m.User.IsDeleted &&
            m.RoleEntity.GroupId == groupId.Value && !m.RoleEntity.IsDeleted &&
            m.RoleEntity.RolePermissions.Any(rp =>
                rp.Permission.PermissionCode == GroupPermissions.Finance.LedgerView), ct);
        return allowed ? groupId : null;
    }

    public async Task<Guid?> ResolveCashRequestSessionIdAsync(Guid feeId, Guid recipientId, CancellationToken ct)
    {
        var target = await _dbContext.SessionFees.AsNoTracking()
            .Where(f => f.Id == feeId && !f.IsDeleted && !f.Session.IsDeleted &&
                f.Session.Group.IsActive && !f.Session.Group.IsDeleted)
            .Select(f => new { f.SessionId, f.Session.GroupId })
            .SingleOrDefaultAsync(ct);
        if (target is null) return null;

        var canManageFees = await _dbContext.GroupMembers.AsNoTracking().AnyAsync(m =>
            m.GroupId == target.GroupId && m.UserId == recipientId &&
            m.Status == ClubMemberStatus.ACTIVE && !m.IsDeleted &&
            m.User.IsActive && !m.User.IsDeleted &&
            m.RoleEntity.GroupId == target.GroupId && !m.RoleEntity.IsDeleted &&
            m.RoleEntity.RolePermissions.Any(rp =>
                rp.Permission.PermissionCode == GroupPermissions.Finance.FeeManage), ct);
        return canManageFees ? target.SessionId : null;
    }
}
