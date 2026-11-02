using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Notifications;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Domain.Exceptions;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.Events;

public sealed class CashPaymentRequestedDomainEventHandler(IPaymentRepository fees, INotificationRepository notifications,
    IOutboxRepository outbox) : INotificationHandler<CashPaymentRequestedDomainEvent>
{
    public async Task Handle(CashPaymentRequestedDomainEvent change, CancellationToken ct)
    {
        var fee = await fees.GetMineAsync(change.FeeId, change.UserId, false, ct)
            ?? throw new PaymentOperationConflictException(new InvalidOperationException("Khoản thu không còn truy cập được."));
        var recipients = await fees.CashRecipientsAsync(change.SessionId, ct);
        if (recipients.Count == 0) throw new PaymentOperationConflictException(new InvalidOperationException("Không còn người quản lý thu tiền hợp lệ."));
        foreach (var user in recipients)
        {
            notifications.Add(new Notification(user, "Yêu cầu xác nhận tiền mặt", "Người chơi yêu cầu xác nhận khoản nộp tiền mặt.",
                NotificationType.PAYMENT, "SessionFee", change.FeeId));
            outbox.Add(new OutboxMessage("CashPaymentRequestedPush", user,
                JsonSerializer.Serialize(new CashPaymentRequestedPush(change.Id, change.RequestId, change.FeeId, user, fee.Session.GroupId, change.SessionId)), change.OccurredAt));
        }
    }
}
