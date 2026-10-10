using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Persistence.Repositories;

namespace LegendsTeamVN.BadmintonClub.Persistence.Repositories;

public class ClubMemberRepository(BadmintonDbContext dbContext) : GenericRepository<BadmintonDbContext, GroupMember, Guid>(dbContext), IClubMemberRepository
{
}
