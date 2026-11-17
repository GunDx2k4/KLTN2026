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

        var recruitments = await recruitmentRepository.FindAll(r => !r.IsDeleted &&
                (r.Status == RecruitmentStatus.ACTIVE || r.Status == RecruitmentStatus.FILLED))
            .Include(r => r.Session)
                .ThenInclude(s => s.Group)
            .Include(r => r.Session.Bookings)
            .Where(r => !r.Session.IsDeleted &&
                r.Session.EndTime > now &&
                (r.Session.Status == SessionStatus.OPEN ||
                 (currentUserId.HasValue && r.Session.Bookings.Any(b =>
                     !b.IsDeleted &&
                     b.UserId == currentUserId.Value &&
                     b.Status != BookingStatus.ABSENT))))
            .OrderBy(r => r.Session.StartTime)
            .ToListAsync(cancellationToken);

        var result = new List<GuestRecruitmentResponse>();

        foreach (var r in recruitments)
        {
            string? myBookingStatus = null;
            if (currentUserId.HasValue)
            {
                var userBooking = r.Session.Bookings.FirstOrDefault(b =>
                    !b.IsDeleted &&
                    b.UserId == currentUserId.Value &&
                    b.Status != BookingStatus.ABSENT);

                if (userBooking != null)
                {
                    myBookingStatus = userBooking.Status.ToString();
                }
            }

            var slotsRemaining = RecruitmentRules.RemainingSlots(r.Session, r);
            if (myBookingStatus == null)
            {
                if (r.Session.Status != SessionStatus.OPEN || r.Status != RecruitmentStatus.ACTIVE || slotsRemaining <= 0)
                {
                    continue;
                }
            }

            if (request.MinSkillLevel.HasValue && r.MinSkillLevel > request.MinSkillLevel.Value)
            {
                continue;
            }

            double? distanceKm = null;
            var sessionLat = r.Session.Lat ?? r.Session.Group?.Lat;
            var sessionLng = r.Session.Lng ?? r.Session.Group?.Lng;
            if (request.Latitude.HasValue && request.Longitude.HasValue && sessionLat.HasValue && sessionLng.HasValue)
            {
                distanceKm = CalculateDistance(
                    request.Latitude.Value,
                    request.Longitude.Value,
                    (double)sessionLat.Value,
                    (double)sessionLng.Value
                );
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
                distanceKm.HasValue ? Math.Round(distanceKm.Value, 1) : null,
                myBookingStatus,
                r.Session.Group?.GroupQualityScore
            ));
        }

        if (request.Latitude.HasValue && request.Longitude.HasValue)
        {
            result = result
                .OrderBy(x => x.DistanceKm.HasValue ? 0 : 1)
                .ThenBy(x => x.DistanceKm.HasValue ? Math.Round(x.DistanceKm.Value, 0) : double.MaxValue)
                .ThenByDescending(x => x.ClubQualityScore ?? 0)
                .ThenBy(x => x.DistanceKm ?? double.MaxValue)
                .ThenBy(x => x.StartTime)
                .ToList();
        }
        else
        {
            result = result
                .OrderByDescending(x => x.ClubQualityScore ?? 0)
                .ThenBy(x => x.StartTime)
                .ToList();
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
