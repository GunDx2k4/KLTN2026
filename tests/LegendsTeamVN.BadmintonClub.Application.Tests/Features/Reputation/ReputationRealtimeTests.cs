using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Notifications;
using LegendsTeamVN.BadmintonClub.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using Xunit;
namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Reputation;
public sealed class ReputationRealtimeTests
{
    [Theory]
    [InlineData("ReviewInvitationPush", "ReviewInvitation")]
    [InlineData("ReputationUpdatedPush", "ReputationUpdated")]
    public async Task PublishesOnlyToRecipientUser(string kind, string eventName)
    {
        var hub = Substitute.For<IHubContext<SessionHub>>();
        var client = Substitute.For<IClientProxy>();
        var push = new ReputationPush(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null);
        hub.Clients.User(push.RecipientId.ToString()).Returns(client);
        await new SignalRReputationRealtimePublisher(hub).PublishAsync(kind, push, default);
        await client.Received(1).SendCoreAsync(eventName, Arg.Is<object?[]>(args => args.Length == 1 && Equals(args[0], push)), Arg.Any<CancellationToken>());
        hub.Clients.DidNotReceiveWithAnyArgs().Group(default!);
        await hub.Clients.DidNotReceive().All.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>());
    }
}
