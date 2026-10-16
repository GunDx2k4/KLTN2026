using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Requests;

public sealed record CreateClubScheduleItemRequest(
    int DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime
);

public sealed record CreateClubRequest(
    string Name,
    string? Description = null,
    string? AvatarUrl = null,
    string? FixedVenueName = null,
    string? FixedAddress = null,
    decimal? Lat = null,
    decimal? Lng = null,
    SkillLevel MinSkillLevel = SkillLevel.NB,
    SkillLevel MaxSkillLevel = SkillLevel.K,
    List<CreateClubScheduleItemRequest>? Schedules = null
);

