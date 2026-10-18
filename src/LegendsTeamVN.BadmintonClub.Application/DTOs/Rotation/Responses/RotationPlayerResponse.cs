using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;

public sealed record RotationPlayerResponse(Guid UserId, string Name, SkillLevel SkillLevel, SideTeam? Team,
    string State, DateTime? CheckedInAt, DateTime? LastMatchEndAt,
    DateTime? EligibleAgainAt, bool IsCoolingDown, bool CanBeProposed);
