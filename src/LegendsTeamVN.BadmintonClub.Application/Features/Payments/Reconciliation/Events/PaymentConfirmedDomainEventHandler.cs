using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Notifications;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.Reconciliation.Events;

public sealed class PaymentConfirmedDomainEventHandler(INotificationRepository notifications, IOutboxRepository outbox)
    : INotificationHandler<PaymentConfirmedDomainEvent>
{
    public Task Handle(PaymentConfirmedDomainEvent change, CancellationToken ct)
    {
        notifications.Add(new Notification(change.UserId, "Đã xác nhận thanh toán", "Khoản đóng của bạn đã được xác nhận.",
            NotificationType.PAYMENT, "SessionFee", change.FeeId));
        outbox.Add(new OutboxMessage("PaymentConfirmedPush", change.UserId,
            JsonSerializer.Serialize(new PaymentConfirmedPush(change.Id, change.FeeId, change.UserId, change.GroupId, change.SessionId)), change.OccurredAt));
        return Task.CompletedTask;
    }
}
