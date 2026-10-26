using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Bookings.GetMyBooking;

public sealed class GetMyBookingQueryHandler(
    IBookingRepository bookingRepository,
    ICurrentUserService currentUserService) : IQueryHandler<GetMyBookingQuery, SessionBookingResponse?>
{
    public async Task<Result<SessionBookingResponse?>> Handle(GetMyBookingQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<SessionBookingResponse?>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập để kiểm tra vé."));
        }

        var booking = await bookingRepository.FindAll(b => !b.IsDeleted && !b.Session.IsDeleted && b.SessionId == request.SessionId && b.UserId == userId.Value && b.Status != BookingStatus.ABSENT)
            .Include(b => b.User)
            .FirstOrDefaultAsync(cancellationToken);

        if (booking is null)
        {
            return Result.Success<SessionBookingResponse?>(null);
        }

        var response = new SessionBookingResponse(
            booking.Id,
            booking.SessionId,
            booking.UserId,
            booking.User != null ? booking.User.FullName : "Unknown",
            booking.User != null ? booking.User.AvatarUrl : null,
            booking.User != null ? booking.User.PhoneNumber : null,
            booking.User != null ? booking.User.SkillLevel.ToString() : "NB",
            booking.Status,
            booking.QueueOrder,
            booking.BookedAt,
            booking.CheckinAt,
            booking.RecruitmentId,
            booking.User != null ? booking.User.Gender : (short)1
        );

        return Result.Success<SessionBookingResponse?>(response);
    }
}
