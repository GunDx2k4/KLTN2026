using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Explore;

public sealed class ExploreClubsQueryHandler(IClubRepository clubRepository) 
    : IQueryHandler<ExploreClubsQuery, List<ExploreClubResponse>>
{
    public async Task<Result<List<ExploreClubResponse>>> Handle(ExploreClubsQuery request, CancellationToken cancellationToken)
    {
        var query = clubRepository.FindAll(c => c.IsActive)
            .Include(c => c.Members)
            .Include(c => c.Schedules)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(c => 
                c.GroupName.ToLower().Contains(term) ||
                (c.FixedVenueName != null && c.FixedVenueName.ToLower().Contains(term)) ||
                (c.FixedAddress != null && c.FixedAddress.ToLower().Contains(term))
            );
        }

        if (request.IsRecruiting.HasValue)
        {
            query = query.Where(c => c.IsRecruiting == request.IsRecruiting.Value);
        }

        if (request.SkillLevel.HasValue)
        {
            var skill = request.SkillLevel.Value;
            query = query.Where(c => c.MinSkillLevel <= skill && c.MaxSkillLevel >= skill);
        }

        var clubs = await query.ToListAsync(cancellationToken);
        var result = new List<ExploreClubResponse>();

        foreach (var c in clubs)
        {
            double? distanceKm = null;
            if (request.Latitude.HasValue && request.Longitude.HasValue && c.Lat.HasValue && c.Lng.HasValue)
            {
                distanceKm = CalculateDistance(
                    request.Latitude.Value,
                    request.Longitude.Value,
                    (double)c.Lat.Value,
                    (double)c.Lng.Value
                );

                if (distanceKm > request.RadiusKm)
                {
                    continue;
                }
            }

            var schedulesSummary = FormatSchedules(c.Schedules.Where(s => s.IsActive).ToList());
            var activeMemberCount = c.Members.Count(m => m.Status == Domain.Enums.ClubMemberStatus.ACTIVE);

            result.Add(new ExploreClubResponse(
                c.Id,
                c.GroupName,
                c.Code,
                c.Description,
                c.AvatarUrl,
                c.FixedVenueName,
                c.FixedAddress,
                distanceKm.HasValue ? Math.Round(distanceKm.Value, 1) : null,
                c.MinSkillLevel,
                c.MaxSkillLevel,
                activeMemberCount,
                c.IsRecruiting,
                c.GroupQualityScore,
                schedulesSummary
            ));
        }

        if (request.Latitude.HasValue && request.Longitude.HasValue)
        {
            result = result.OrderBy(c => c.DistanceKm ?? double.MaxValue).ToList();
        }
        else
        {
            result = result.OrderByDescending(c => c.QualityScore).ThenBy(c => c.GroupName).ToList();
        }

        return Result.Success(result);
    }

    private static string? FormatSchedules(List<GroupSchedule> schedules)
    {
        if (schedules.Count == 0) return null;

        var days = schedules.OrderBy(s => s.DayOfWeek).Select(s => s.DayOfWeek switch
        {
            0 => "CN",
            1 => "T2",
            2 => "T3",
            3 => "T4",
            4 => "T5",
            5 => "T6",
            6 => "T7",
            _ => $"T{s.DayOfWeek + 1}"
        });

        var first = schedules.First();
        return $"{string.Join(", ", days)} ({first.StartTime:HH\\:mm} - {first.EndTime:HH\\:mm})";
    }

    private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;

        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double deg) => deg * (Math.PI / 180.0);
}
