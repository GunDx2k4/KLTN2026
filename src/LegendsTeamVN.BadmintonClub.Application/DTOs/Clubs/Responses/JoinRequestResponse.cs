using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;

public sealed record JoinRequestResponse(
    Guid Id,
    Guid ClubId,
    Guid UserId,
    string? FullName,
    string? Email,
    string? PhoneNumber,
    string? AvatarUrl,
    string? Introduction,
    JoinRequestStatus Status,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    Guid? ApprovedBy);
