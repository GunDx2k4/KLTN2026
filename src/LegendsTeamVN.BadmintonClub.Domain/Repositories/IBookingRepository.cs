using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.Core.Domain.Repositories;

namespace LegendsTeamVN.BadmintonClub.Domain.Repositories;

public interface IBookingRepository : IGenericRepository<SessionBooking, Guid>
{
    Task<bool> HasOverlappingBookingAsync(
        Guid userId,
        DateTime startTime,
        DateTime endTime,
        Guid? excludeSessionId = null,
        CancellationToken cancellationToken = default);
}
