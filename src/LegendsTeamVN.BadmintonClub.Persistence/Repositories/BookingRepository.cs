using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Persistence.Repositories;

namespace LegendsTeamVN.BadmintonClub.Persistence.Repositories;

public class BookingRepository(BadmintonDbContext dbContext) 
    : GenericRepository<BadmintonDbContext, SessionBooking, Guid>(dbContext), IBookingRepository
{
}
