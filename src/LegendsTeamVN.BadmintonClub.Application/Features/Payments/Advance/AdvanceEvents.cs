using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.Advance;

public sealed record AdvancePush(Guid SessionId, string Title, string Body)
{
    public const string Kind = "SessionAdvanceNotification";
}

public sealed class AdvanceChangedDomainEventHandler(INotificationRepository notifications, IOutboxRepository outbox) : INotificationHandler<SessionAdvanceChangedDomainEvent>
{
    public Task Handle(SessionAdvanceChangedDomainEvent change, CancellationToken ct)
    {
        var title = change.Status switch
        {
            nameof(SessionAdvanceStatus.REFUNDED) => "Đã hoàn tiền",
            nameof(SessionAdvanceStatus.RECEIVED) => "Đã nhận khoản ứng",
            _ => "Chờ hoàn tiền"
        };
        var body = $"Buổi {change.SessionTitle} · {change.Amount:N0} đ. Xem chi tiết trong Khoản phí của tôi.";
        notifications.Add(new Notification(change.UserId, title, body,
            NotificationType.PAYMENT, "SessionAdvance", change.SessionId));
        outbox.Add(new OutboxMessage(AdvancePush.Kind, change.UserId,
            JsonSerializer.Serialize(new AdvancePush(change.SessionId, title, body)), change.OccurredAt));
        return Task.CompletedTask;
    }
}
