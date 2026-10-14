using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Persistence.Repositories;

namespace LegendsTeamVN.BadmintonClub.Persistence.Repositories;

public class RecruitmentRepository(BadmintonDbContext dbContext) 
    : GenericRepository<BadmintonDbContext, GuestRecruitment, Guid>(dbContext), IRecruitmentRepository
{
}
