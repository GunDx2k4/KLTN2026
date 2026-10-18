using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;

public sealed record RotationMatchResponse(Guid MatchId, int CourtNumber, int RoundNumber, MatchStatus Status,
    DateTime? TimeStart, DateTime? TimeEnd, IReadOnlyList<RotationPlayerResponse> Players);
