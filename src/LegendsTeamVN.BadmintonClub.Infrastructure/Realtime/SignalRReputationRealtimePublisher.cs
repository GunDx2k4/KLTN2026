using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Notifications;
using Microsoft.AspNetCore.SignalR;
namespace LegendsTeamVN.BadmintonClub.Infrastructure.Realtime;
public sealed class SignalRReputationRealtimePublisher(IHubContext<SessionHub> hub) : IReputationRealtimePublisher
{
    public Task PublishAsync(string kind, ReputationPush push, CancellationToken ct)
    {
        var eventName = kind switch
        {
            "ReviewInvitationPush" => "ReviewInvitation",
            "ReputationUpdatedPush" => "ReputationUpdated",
            _ => throw new ArgumentException("Loại thông báo uy tín không hợp lệ.", nameof(kind))
        };
        return hub.Clients.User(push.RecipientId.ToString()).SendAsync(eventName, push, ct);
    }
}
