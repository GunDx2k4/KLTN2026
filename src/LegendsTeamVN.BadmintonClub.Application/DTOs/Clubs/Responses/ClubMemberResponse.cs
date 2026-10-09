using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;

public record ClubMemberResponse(
    Guid Id,
    Guid ClubId,
    Guid UserId,
    string? UserName,
    string? Email,
    ClubRole Role,
    ClubMemberStatus Status,
    string? Nickname,
    DateTime JoinedAt
);
