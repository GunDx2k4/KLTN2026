using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Persistence.Repositories;

namespace LegendsTeamVN.BadmintonClub.Persistence.Repositories;

public class GroupRoleRepository(BadmintonDbContext dbContext) : GenericRepository<BadmintonDbContext, GroupRole, Guid>(dbContext), IGroupRoleRepository
{
}
