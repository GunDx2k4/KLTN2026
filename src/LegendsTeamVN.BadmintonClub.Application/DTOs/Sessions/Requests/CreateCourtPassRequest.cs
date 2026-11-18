using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;

public sealed record CreateCourtPassRequest(
    Guid? ClubId,
    string CourtLocation,
    string? Address,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    int CourtCount = 1,
    decimal PassPrice = 0,
    string? ContactPhone = null,
    string? Note = null,
    decimal? Lat = null,
    decimal? Lng = null,
    SkillLevel? MinSkillLevel = SkillLevel.NB
);
