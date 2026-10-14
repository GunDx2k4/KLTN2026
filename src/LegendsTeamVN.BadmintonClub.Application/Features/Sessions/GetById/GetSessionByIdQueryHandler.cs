using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.GetById;

public sealed class GetSessionByIdQueryHandler(
    ISessionRepository sessionRepository,
    ICurrentUserService currentUserService) : IQueryHandler<GetSessionByIdQuery, SessionDetailResponse>
{
    public async Task<Result<SessionDetailResponse>> Handle(GetSessionByIdQuery request, CancellationToken cancellationToken)
    {
        var session = await sessionRepository.FindAll(s => s.Id == request.SessionId)
            .Include(s => s.Group)
            .Include(s => s.Bookings)
                .ThenInclude(b => b.User)
            .Include(s => s.Recruitments)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return Result.Failure<SessionDetailResponse>(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));
        }

        var currentUserId = currentUserService.UserId;
        var myBooking = currentUserId.HasValue 
            ? session.Bookings.FirstOrDefault(b => b.UserId == currentUserId.Value && b.Status != BookingStatus.ABSENT) 
            : null;

        var confirmedBookings = session.Bookings
            .Where(b => b.Status == BookingStatus.CONFIRMED || b.Status == BookingStatus.CHECKED_IN)
            .OrderBy(b => b.BookedAt)
            .Select(b => new SessionBookingResponse(
                b.Id,
                b.SessionId,
                b.UserId,
                b.User?.FullName ?? "Unknown",
                b.User?.AvatarUrl,
                b.User?.PhoneNumber,
                b.User?.SkillLevel.ToString() ?? "NB",
                b.Status,
                b.QueueOrder,
                b.BookedAt,
                b.CheckinAt,
                b.RecruitmentId
            ))
            .ToList();

        var waitingBookings = session.Bookings
            .Where(b => b.Status == BookingStatus.WAITING)
            .OrderBy(b => b.QueueOrder)
            .Select(b => new SessionBookingResponse(
                b.Id,
                b.SessionId,
                b.UserId,
                b.User?.FullName ?? "Unknown",
                b.User?.AvatarUrl,
                b.User?.PhoneNumber,
                b.User?.SkillLevel.ToString() ?? "NB",
                b.Status,
                b.QueueOrder,
                b.BookedAt,
                b.CheckinAt,
                b.RecruitmentId
            ))
            .ToList();

        var activeRecruitment = session.Recruitments
            .Where(r => r.Status == RecruitmentStatus.ACTIVE)
            .OrderByDescending(r => r.BroadcastAt)
            .Select(r => new GuestRecruitmentResponse(
                r.Id,
                r.SessionId,
                session.Group?.GroupName ?? string.Empty,
                session.CourtLocation,
                session.StartTime,
                session.EndTime,
                r.SlotsNeeded,
                Math.Max(0, r.SlotsNeeded - session.Bookings.Count(b => b.RecruitmentId == r.Id && b.Status == BookingStatus.CONFIRMED)),
                r.MinSkillLevel,
                r.SharedCost,
                r.ScanRadiusKm,
                r.Status,
                r.BroadcastAt
            ))
            .FirstOrDefault();

        var sessionDto = new SessionResponse(
            session.Id,
            session.GroupId,
            session.Group?.GroupName ?? string.Empty,
            session.Title,
            session.StartTime,
            session.EndTime,
            session.CourtLocation,
            session.CourtCount,
            session.TargetPlayerPerCourt,
            session.MaxPlayers,
            session.DeadlineBooking,
            session.CourtRentalCost,
            session.Status,
            confirmedBookings.Count,
            waitingBookings.Count,
            myBooking != null,
            myBooking?.Status,
            myBooking?.QueueOrder ?? 0
        );

        return Result.Success(new SessionDetailResponse(sessionDto, confirmedBookings, waitingBookings, activeRecruitment));
    }
}
