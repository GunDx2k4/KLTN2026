using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;

public sealed record CreateRecruitmentRequest(
    int SlotsNeeded,
    SkillLevel MinSkillLevel = SkillLevel.NB,
    decimal SharedCost = 0,
    decimal ScanRadiusKm = 5.0m
);
