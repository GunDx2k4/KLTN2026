using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Persistence.Repositories;

public class BookingRepository(BadmintonDbContext dbContext) 
    : GenericRepository<BadmintonDbContext, SessionBooking, Guid>(dbContext), IBookingRepository
{
    public async Task<bool> HasOverlappingBookingAsync(
        Guid userId,
        DateTime startTime,
        DateTime endTime,
        Guid? excludeSessionId = null,
        CancellationToken cancellationToken = default)
    {
        var startUtc = startTime.Kind == DateTimeKind.Utc ? startTime : DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
        var endUtc = endTime.Kind == DateTimeKind.Utc ? endTime : DateTime.SpecifyKind(endTime, DateTimeKind.Utc);

        var buffer = (endUtc - startUtc) > TimeSpan.FromMinutes(5)
            ? TimeSpan.FromMinutes(1)
            : TimeSpan.FromSeconds(5);
        var effectiveStart = startUtc.Add(buffer);
        var effectiveEnd = endUtc.Subtract(buffer);

        var query = from b in DbContext.SessionBookings.AsNoTracking()
                    join s in DbContext.BadmintonSessions.AsNoTracking() on b.SessionId equals s.Id
                    where !b.IsDeleted
                          && b.UserId == userId
                          && (b.Status == BookingStatus.CONFIRMED || b.Status == BookingStatus.CHECKED_IN)
                          && !s.IsDeleted
                          && s.Status != SessionStatus.COMPLETED
                          && s.Status != SessionStatus.CALCULATED
                          && s.StartTime < effectiveEnd
                          && s.EndTime > effectiveStart
                    select b;

        if (excludeSessionId.HasValue)
        {
            query = query.Where(b => b.SessionId != excludeSessionId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }
}
