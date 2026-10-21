namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Requests;

public sealed record UpdateManualLineupRequest(IReadOnlyList<Guid>? TeamA, IReadOnlyList<Guid>? TeamB,
    long? ExpectedRotationVersion);
