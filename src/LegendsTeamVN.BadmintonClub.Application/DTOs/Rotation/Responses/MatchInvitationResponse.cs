namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;

public sealed record MatchInvitationResponse(Guid EventId, Guid SessionId, Guid MatchId, int CourtNumber,
    DateTime TimeStart, long RotationVersion, PersonalMatchPlayerResponse Teammate,
    IReadOnlyList<PersonalMatchPlayerResponse> Opponents);
