using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Notifications;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.Reconciliation.Events;

public sealed class ReconciliationRequiredDomainEventHandler(IReconciliationRepository repository,
    INotificationRepository notifications, IOutboxRepository outbox) : INotificationHandler<ReconciliationRequiredDomainEvent>
{
    public async Task Handle(ReconciliationRequiredDomainEvent change, CancellationToken ct)
    {
        foreach (var recipient in await repository.ManagersAsync(change.GroupId, ct))
        {
            notifications.Add(new Notification(recipient, "Giao dịch cần đối soát", "Có giao dịch ngân hàng cần kiểm tra bằng chứng.",
                NotificationType.PAYMENT, "BankTransaction", change.TransactionId));
            outbox.Add(new OutboxMessage("ReconciliationAlertPush", recipient,
                JsonSerializer.Serialize(new ReconciliationAlertPush(change.Id, change.TransactionId, recipient, change.GroupId)), change.OccurredAt));
        }
    }
}
