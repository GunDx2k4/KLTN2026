using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Notifications;

namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;

public interface ILedgerRealtimePublisher
{
    Task PublishAsync(LedgerUpdatedPush push, CancellationToken ct);
}
