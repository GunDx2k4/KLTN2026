using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Notifications;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using MediatR;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger.Events;

public sealed class LedgerRecordedDomainEventHandler(ILedgerWriteRepository repository, IOptionsMonitor<LedgerOptions> options)
    : INotificationHandler<LedgerRecordedDomainEvent>
{
    public async Task Handle(LedgerRecordedDomainEvent change, CancellationToken ct)
    {
        if (!options.CurrentValue.Enabled) return;
        foreach (var recipient in await repository.GetViewerIdsAsync(change.GroupId, ct))
        {
            repository.Add(new Notification(recipient, "Sổ quỹ được cập nhật", "Nhóm vừa ghi nhận một khoản thu hoặc chi.",
                NotificationType.PAYMENT, "GroupLedger", change.LedgerId));
            repository.Add(new OutboxMessage("LedgerUpdatedPush", recipient,
                JsonSerializer.Serialize(new LedgerUpdatedPush(change.Id, change.GroupId, change.LedgerId, recipient)), change.OccurredAt));
        }
    }
}
