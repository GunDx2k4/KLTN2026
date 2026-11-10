namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Notifications;

public sealed record NotificationResponse(
    Guid Id,
    string Title,
    string Content,
    string Type,
    string? ReferenceType,
    Guid? ReferenceId,
    bool IsRead,
    DateTime CreatedAt
);

