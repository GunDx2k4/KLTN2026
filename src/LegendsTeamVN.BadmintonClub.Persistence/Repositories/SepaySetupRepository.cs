using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Identity.Authorization;
using LegendsTeamVN.Core.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Persistence.Repositories;

public sealed class SepaySetupRepository(BadmintonDbContext db, AppIdentityDbContext identity) : ISepaySetupRepository
{
    public Task<SepayApplicationSettings?> GetApplicationAsync(CancellationToken ct) =>
        db.Set<SepayApplicationSettings>().SingleOrDefaultAsync(x => x.Id == 1, ct);

    public async Task<bool> CanManageApplicationAsync(Guid userId, CancellationToken ct) =>
        await db.AppUsers.AnyAsync(x => x.Id == userId && x.IsActive && !x.IsDeleted, ct) &&
        await identity.Users.AnyAsync(x => x.Id == userId && (x.LockoutEnd == null || x.LockoutEnd <= DateTimeOffset.UtcNow), ct) &&
        await identity.UserRoles.Where(x => x.UserId == userId).Join(identity.RolePermissions,
            userRole => userRole.RoleId, rolePermission => rolePermission.RoleId, (_, p) => p)
            .AnyAsync(x => x.Permission.Name == AppPermissions.Integrations.SepayManage, ct);

    public Task<ClubSepayConnection?> GetConnectionAsync(Guid bankAccountId, CancellationToken ct) =>
        db.Set<ClubSepayConnection>().Where(x => x.BankAccountId == bankAccountId)
            .OrderBy(x => x.Status == "DISCONNECTED").ThenByDescending(x => x.AuthorizedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);

    public Task<ClubSepayConnection?> GetConnectionByIdAsync(Guid connectionId, CancellationToken ct) =>
        db.Set<ClubSepayConnection>().SingleOrDefaultAsync(x => x.Id == connectionId, ct);

    public async Task<ClubSepayConnection?> GetWebhookConnectionAsync(Guid bankAccountId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '3s'", ct);
            return await db.Set<ClubSepayConnection>().FromSqlInterpolated(
                $"SELECT * FROM club_sepay_connection WHERE \"BankAccountId\" = {bankAccountId} AND \"Status\" = 'CONNECTED' FOR SHARE")
                .AsNoTracking().SingleOrDefaultAsync(ct);
        }
        return await db.Set<ClubSepayConnection>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.BankAccountId == bankAccountId && x.Status == "CONNECTED", ct);
    }
    public async Task<IReadOnlyList<ClubSepayConnection>> ListConnectionsAsync(Guid groupId, CancellationToken ct) =>
        await db.Set<ClubSepayConnection>().AsNoTracking().Where(x => x.GroupId == groupId && x.Status != "DISCONNECTED")
            .OrderByDescending(x => x.AuthorizedAt).ToListAsync(ct);
    public Task<bool> HasConnectionsAsync(CancellationToken ct) =>
        db.Set<ClubSepayConnection>().AnyAsync(x => x.Status != "DISCONNECTED", ct);
    public Task<bool> IsProviderBankInUseAsync(long companyId, long providerBankId, Guid connectionId, CancellationToken ct) =>
        db.Set<ClubSepayConnection>().AnyAsync(x => x.CompanyId == companyId && x.ProviderBankAccountId == providerBankId &&
            x.Id != connectionId && x.Status != "DISCONNECTED", ct);

    public void AddApplication(SepayApplicationSettings settings) => db.Add(settings);
    public void AddConnection(ClubSepayConnection connection) => db.Add(connection);

    public async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new SepaySetupException("Busy", "Cấu hình đã thay đổi. Vui lòng tải lại."); }
        catch (DbUpdateException) { throw new SepaySetupException("Busy", "Chưa lưu được kết nối. Vui lòng tải lại trạng thái."); }
    }
}
