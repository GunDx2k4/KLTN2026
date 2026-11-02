using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;

public sealed record RotationPlayerResponse(Guid UserId, string Name, SkillLevel SkillLevel, SideTeam? Team,
    string State, DateTime? CheckedInAt, DateTime? LastMatchEndAt,
    DateTime? EligibleAgainAt, bool IsCoolingDown, bool CanBeProposed, bool IsManualSwapped = false,
    bool IsRotationPaused = false, bool CanPauseRotation = false, bool CanResumeRotation = false,
    string? PauseSource = null, string? PauseResumePolicy = null, Guid? SelfPauseTargetMatchId = null,
    string? SelfPauseProgress = null);
