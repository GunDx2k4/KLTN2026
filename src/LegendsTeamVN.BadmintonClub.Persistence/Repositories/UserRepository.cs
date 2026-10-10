using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Persistence.Repositories;

public class UserRepository(BadmintonDbContext dbContext) : GenericRepository<BadmintonDbContext, AppUser, Guid>(dbContext), IUserRepository
{
    public async Task<bool> IsUserExistAsync(string? phone, string? email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(email))
            return false;

        return await DbContext.AppUsers.AsNoTracking().AnyAsync(u =>
            (!string.IsNullOrEmpty(phone) && u.PhoneNumber == phone) ||
            (!string.IsNullOrEmpty(email) && u.EmailAddress == email),
            cancellationToken);
    }

    public async Task<AppUser?> FindByEmailOrPhoneAsync(string emailOrPhone, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(emailOrPhone))
            return null;

        return await DbContext.AppUsers.FirstOrDefaultAsync(u =>
            u.PhoneNumber == emailOrPhone || u.EmailAddress == emailOrPhone,
            cancellationToken);
    }

    public async Task<AppUser?> GetUserWithMembershipsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DbContext.AppUsers
            .Include(u => u.GroupMemberships.Where(m => m.Status == Domain.Enums.ClubMemberStatus.ACTIVE))
                .ThenInclude(m => m.Group)
            .Include(u => u.GroupMemberships.Where(m => m.Status == Domain.Enums.ClubMemberStatus.ACTIVE))
                .ThenInclude(m => m.RoleEntity)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
    }
}
