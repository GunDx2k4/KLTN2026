namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;

public sealed record LineupSnapshotResponse(int SchemaVersion, IReadOnlyList<Guid> TeamA, IReadOnlyList<Guid> TeamB);
