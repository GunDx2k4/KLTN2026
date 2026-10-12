using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Persistence.Repositories;

namespace LegendsTeamVN.BadmintonClub.Persistence.Repositories;

public class JoinRequestRepository(BadmintonDbContext dbContext) : GenericRepository<BadmintonDbContext, JoinRequest, Guid>(dbContext), IJoinRequestRepository
{
}
