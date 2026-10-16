using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Bookings.GetMyBookings;

public sealed class GetMyBookingsQueryHandler(
    IBookingRepository bookingRepository,
    ICurrentUserService currentUserService) : IQueryHandler<GetMyBookingsQuery, List<MyBookingResponse>>
{
    public async Task<Result<List<MyBookingResponse>>> Handle(GetMyBookingsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<List<MyBookingResponse>>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập để xem lịch đã đăng ký."));
        }

        var bookings = await bookingRepository
            .FindAll(b => b.UserId == userId.Value && b.Status != BookingStatus.ABSENT)
            .Include(b => b.Session)
                .ThenInclude(s => s.Group)
            .OrderByDescending(b => b.Session.StartTime)
            .ToListAsync(cancellationToken);

        var response = bookings.Select(b => new MyBookingResponse(
            BookingId: b.Id,
            SessionId: b.SessionId,
            SessionTitle: b.Session.Title,
            ClubId: b.Session.GroupId,
            ClubName: b.Session.Group.GroupName,
            ClubAvatarUrl: b.Session.Group.AvatarUrl,
            StartTime: b.Session.StartTime,
            EndTime: b.Session.EndTime,
            CourtLocation: b.Session.CourtLocation,
            SessionStatus: b.Session.Status,
            BookingStatus: b.Status,
            QueueOrder: b.QueueOrder,
            FeePerMember: b.Session.FeePerMember,
            BookedAt: b.BookedAt,
            CheckinAt: b.CheckinAt
        )).ToList();

        return Result.Success(response);
    }
}
