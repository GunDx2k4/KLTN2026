using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Notifications;
namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;
public interface IReputationRealtimePublisher
{
    Task PublishAsync(string kind, ReputationPush push, CancellationToken ct);
}
