using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Settlement.Events;

public sealed record SettlementPaymentPush(Guid EventId, Guid SessionId, Guid FeeId, Guid UserId, decimal Amount, string PaymentCode);

public sealed class SettlementPublishedDomainEventHandler(ISessionRepository sessions,
    INotificationRepository notifications, IOutboxRepository outbox) : INotificationHandler<SettlementPublishedDomainEvent>
{
    public async Task Handle(SettlementPublishedDomainEvent change, CancellationToken ct)
    {
        var session = await sessions.FindByIdAsync(change.SessionId, ct, s => s.Fees)
            ?? throw new InvalidOperationException("Không tìm thấy buổi chơi vừa phát hành.");
        foreach (var fee in session.Fees.Where(f => change.FeeIds.Contains(f.Id) && f.Amount > 0))
        {
            notifications.Add(new Notification(fee.UserId, "Khoản thu buổi chơi",
                "Khoản thu đã được phát hành. Vui lòng xem chi tiết thanh toán.",
                NotificationType.PAYMENT, "SessionFee", fee.Id));
            var payload = new SettlementPaymentPush(change.Id, session.Id, fee.Id, fee.UserId, fee.Amount, fee.PaymentCode);
            outbox.Add(new OutboxMessage("SettlementPaymentPush", fee.UserId, JsonSerializer.Serialize(payload), change.OccurredAt));
        }
    }
}
