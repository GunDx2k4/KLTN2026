using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Recruitments.Explore;

public sealed class ExploreRecruitmentsQueryHandler(
    IRecruitmentRepository recruitmentRepository,
    ICurrentUserService currentUserService) 
    : IQueryHandler<ExploreRecruitmentsQuery, List<GuestRecruitmentResponse>>
{
    public async Task<Result<List<GuestRecruitmentResponse>>> Handle(ExploreRecruitmentsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var currentUserId = currentUserService.UserId;

        var recruitments = await recruitmentRepository.FindAll(r => !r.IsDeleted && r.Status == RecruitmentStatus.ACTIVE)
            .Include(r => r.Session)
                .ThenInclude(s => s.Group)
            .Include(r => r.Session.Bookings)
            .Where(r => !r.Session.IsDeleted && r.Session.Status == SessionStatus.OPEN && r.Session.EndTime > now)
            .OrderBy(r => r.Session.StartTime)
            .ToListAsync(cancellationToken);

        var result = new List<GuestRecruitmentResponse>();

        foreach (var r in recruitments)
        {
            if (currentUserId.HasValue)
            {
                var hasActiveBooking = r.Session.Bookings.Any(b =>
                    !b.IsDeleted &&
                    b.UserId == currentUserId.Value &&
                    b.Status != BookingStatus.ABSENT);

                if (hasActiveBooking)
                {
                    continue;
                }
            }

            var slotsRemaining = RecruitmentRules.RemainingSlots(r.Session, r);
            if (slotsRemaining <= 0)
            {
                continue;
            }

            if (request.MinSkillLevel.HasValue && r.MinSkillLevel > request.MinSkillLevel.Value)
            {
                continue;
            }

            double? distanceKm = null;
            if (request.Latitude.HasValue && request.Longitude.HasValue && r.Session.Group?.Lat.HasValue == true && r.Session.Group?.Lng.HasValue == true)
            {
                distanceKm = CalculateDistance(
                    request.Latitude.Value,
                    request.Longitude.Value,
                    (double)r.Session.Group.Lat.Value,
                    (double)r.Session.Group.Lng.Value
                );

                if (distanceKm > request.RadiusKm)
                {
                    continue;
                }
            }

            result.Add(new GuestRecruitmentResponse(
                r.Id,
                r.SessionId,
                r.Session.Group?.GroupName ?? string.Empty,
                r.Session.CourtLocation,
                r.Session.StartTime,
                r.Session.EndTime,
                r.SlotsNeeded,
                slotsRemaining,
                r.MinSkillLevel,
                r.SharedCost,
                r.ScanRadiusKm,
                r.Status,
                r.BroadcastAt,
                distanceKm.HasValue ? Math.Round(distanceKm.Value, 1) : null
            ));
        }

        if (request.Latitude.HasValue && request.Longitude.HasValue)
        {
            result = result.OrderBy(x => x.DistanceKm ?? double.MaxValue).ToList();
        }

        return Result.Success(result);
    }

    private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // Earth radius in kilometers
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
