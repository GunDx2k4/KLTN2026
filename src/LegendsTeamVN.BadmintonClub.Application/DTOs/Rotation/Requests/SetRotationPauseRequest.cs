namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Requests;

public sealed record SetRotationPauseRequest(bool? IsPaused, long? ExpectedRotationVersion, Guid? ReplacementUserId = null);
