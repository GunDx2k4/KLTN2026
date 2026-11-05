using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Realtime;

public sealed class SignalRLedgerRealtimePublisher(IHubContext<SessionHub> hub) : ILedgerRealtimePublisher
{
    public Task PublishAsync(LedgerUpdatedPush push, CancellationToken ct) =>
        hub.Clients.User(push.RecipientId.ToString()).SendAsync("LedgerUpdated", push, ct);
}
