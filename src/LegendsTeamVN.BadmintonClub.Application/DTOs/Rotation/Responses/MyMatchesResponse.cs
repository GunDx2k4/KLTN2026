namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;

public sealed record MyMatchesResponse(Guid SessionId, string SessionStatus, string Phase, long RotationVersion,
    DateTime ServerTime, string PlayerState, PersonalMatchResponse? CurrentMatch, PersonalMatchResponse? NextMatch,
    DateTime? RestStartedAt, long? RestElapsedSeconds, DateTime? EligibleAgainAt, long RemainingCooldownSeconds,
    int? QueuePriority, string AvailabilityReason, DateTime? EstimatedStartAt, string EstimateReason,
    bool CanRequestPause, bool CanResumeSelf, string? PauseSource, string? PauseResumePolicy,
    Guid? SelfPauseTargetMatchId, string? SelfPauseProgress);

public sealed record PersonalMatchResponse(Guid MatchId, int CourtNumber, int RoundNumber, string Status,
    DateTime? TimeStart, PersonalMatchPlayerResponse Teammate, IReadOnlyList<PersonalMatchPlayerResponse> Opponents);

public sealed record PersonalMatchPlayerResponse(Guid UserId, string Name);
