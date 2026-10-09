using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Persistence.Repositories;

namespace LegendsTeamVN.BadmintonClub.Persistence.Repositories;

public class ClubRepository(BadmintonDbContext dbContext) : GenericRepository<BadmintonDbContext, Club, Guid>(dbContext), IClubRepository
{
}
