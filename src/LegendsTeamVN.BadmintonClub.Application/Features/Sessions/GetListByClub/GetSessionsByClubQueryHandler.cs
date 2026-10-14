using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.GetListByClub;

public sealed class GetSessionsByClubQueryHandler(
    ISessionRepository sessionRepository,
    ICurrentUserService currentUserService) : IQueryHandler<GetSessionsByClubQuery, List<SessionResponse>>
{
    public async Task<Result<List<SessionResponse>>> Handle(GetSessionsByClubQuery request, CancellationToken cancellationToken)
    {
        var currentUserId = currentUserService.UserId;

        var sessions = await sessionRepository.FindAll(s => s.GroupId == request.ClubId)
            .Include(s => s.Group)
            .Include(s => s.Bookings)
            .OrderByDescending(s => s.StartTime)
            .ToListAsync(cancellationToken);

        var result = sessions.Select(s =>
        {
            var confirmedCount = s.Bookings.Count(b => b.Status == BookingStatus.CONFIRMED || b.Status == BookingStatus.CHECKED_IN);
            var waitingCount = s.Bookings.Count(b => b.Status == BookingStatus.WAITING);
            var myBooking = currentUserId.HasValue 
                ? s.Bookings.FirstOrDefault(b => b.UserId == currentUserId.Value && b.Status != BookingStatus.ABSENT) 
                : null;

            return new SessionResponse(
                s.Id,
                s.GroupId,
                s.Group?.GroupName ?? string.Empty,
                s.Title,
                s.StartTime,
                s.EndTime,
                s.CourtLocation,
                s.CourtCount,
                s.TargetPlayerPerCourt,
                s.MaxPlayers,
                s.DeadlineBooking,
                s.CourtRentalCost,
                s.Status,
                confirmedCount,
                waitingCount,
                myBooking != null,
                myBooking?.Status,
                myBooking?.QueueOrder ?? 0
            );
        }).ToList();

        return Result.Success(result);
    }
}
