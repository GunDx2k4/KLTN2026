using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Persistence.Repositories;

public class ClubRepository(BadmintonDbContext dbContext) : GenericRepository<BadmintonDbContext, ClubGroup, Guid>(dbContext), IClubRepository
{
    public async Task<List<GroupPermission>> GetAllPermissionsAsync(CancellationToken cancellationToken = default)
    {
        return await DbContext.GroupPermissions.AsNoTracking().ToListAsync(cancellationToken);
    }
}
