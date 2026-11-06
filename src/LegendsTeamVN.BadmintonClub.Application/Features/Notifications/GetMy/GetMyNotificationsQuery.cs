using LegendsTeamVN.BadmintonClub.Application.DTOs.Notifications;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Notifications.GetMy;

public sealed record GetMyNotificationsQuery : IQuery<IReadOnlyList<NotificationResponse>>;

