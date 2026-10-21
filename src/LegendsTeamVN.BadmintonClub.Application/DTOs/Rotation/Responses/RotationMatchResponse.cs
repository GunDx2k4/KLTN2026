using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;

public sealed record RotationMatchResponse(Guid MatchId, int CourtNumber, int RoundNumber, MatchStatus Status,
    DateTime? TimeStart, DateTime? TimeEnd, IReadOnlyList<RotationPlayerResponse> Players,
    LineupSnapshotResponse? OriginalLineup = null, int TeamATotal = 0, int TeamBTotal = 0,
    int SkillDifference = 0, bool HasManualIntervention = false, bool CanEditManually = false,
    bool CanRestoreProposal = false, bool CanConfirmManualStart = false);
