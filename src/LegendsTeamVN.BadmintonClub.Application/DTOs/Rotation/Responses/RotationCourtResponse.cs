using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;

public sealed record RotationCourtResponse(int CourtNumber, string State, RotationMatchResponse? Match);
