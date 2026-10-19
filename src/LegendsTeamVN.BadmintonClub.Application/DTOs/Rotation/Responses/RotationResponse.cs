using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;

public sealed record RotationResponse(Guid SessionId, SessionStatus SessionStatus, long RotationVersion,
    DateTime ServerTime, DateTime? RotationStartedAt, bool CanInitialize, bool CanGenerateProposals,
    string? Reason, IReadOnlyList<RotationCourtResponse> Courts, IReadOnlyList<RotationPlayerResponse> Players,
    int MinRestMinutes, DateTime? NextProposalCheckAt, int ReadyCandidateCount, int CoolingDownCount);
