using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Notifications.MarkRead;

public sealed record MarkNotificationAsReadCommand(Guid NotificationId) : ICommand;

