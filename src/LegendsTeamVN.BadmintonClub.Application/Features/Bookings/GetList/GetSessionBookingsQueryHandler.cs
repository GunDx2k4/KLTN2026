using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Bookings.GetList;

public sealed class GetSessionBookingsQueryHandler(IBookingRepository bookingRepository) 
    : IQueryHandler<GetSessionBookingsQuery, List<SessionBookingResponse>>
{
    public async Task<Result<List<SessionBookingResponse>>> Handle(GetSessionBookingsQuery request, CancellationToken cancellationToken)
    {
        var bookings = await bookingRepository.FindAll(b => b.SessionId == request.SessionId && b.Status != BookingStatus.ABSENT)
            .Include(b => b.User)
            .OrderBy(b => b.Status == BookingStatus.CONFIRMED ? 0 : 1)
            .ThenBy(b => b.QueueOrder)
            .ThenBy(b => b.BookedAt)
            .Select(b => new SessionBookingResponse(
                b.Id,
                b.SessionId,
                b.UserId,
                b.User != null ? b.User.FullName : "Unknown",
                b.User != null ? b.User.AvatarUrl : null,
                b.User != null ? b.User.PhoneNumber : null,
                b.User != null ? b.User.SkillLevel.ToString() : "NB",
                b.Status,
                b.QueueOrder,
                b.BookedAt,
                b.CheckinAt,
                b.RecruitmentId
            ))
            .ToListAsync(cancellationToken);

        return Result.Success(bookings);
    }
}
