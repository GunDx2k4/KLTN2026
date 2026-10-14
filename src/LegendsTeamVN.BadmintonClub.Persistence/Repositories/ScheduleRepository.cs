using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Persistence.Repositories;

namespace LegendsTeamVN.BadmintonClub.Persistence.Repositories;

public class ScheduleRepository(BadmintonDbContext context) 
    : GenericRepository<BadmintonDbContext, GroupSchedule, Guid>(context), IScheduleRepository
{
}
