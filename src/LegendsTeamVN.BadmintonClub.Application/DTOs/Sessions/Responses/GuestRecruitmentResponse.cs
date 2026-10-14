using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;

public sealed record GuestRecruitmentResponse(
    Guid Id,
    Guid SessionId,
    string ClubName,
    string CourtLocation,
    DateTime StartTime,
    DateTime EndTime,
    int SlotsNeeded,
    int SlotsRemaining,
    SkillLevel MinSkillLevel,
    decimal SharedCost,
    decimal ScanRadiusKm,
    RecruitmentStatus Status,
    DateTime BroadcastAt,
    double? DistanceKm = null
);
