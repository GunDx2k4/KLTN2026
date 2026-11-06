using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Notifications;
using LegendsTeamVN.BadmintonClub.Application.Tests.Common;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Reputation;
public sealed class ReputationDeliveryTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task DeliveryRechecksSourceAndRetriesWithoutChangingEvent(bool allowed, bool fail)
    {
        var push = new ReputationPush(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ReputationEngineTests.Now.AddDays(7));
        var message = new OutboxMessage("ReviewInvitationPush", push.RecipientId, JsonSerializer.Serialize(push), ReputationEngineTests.Now);
        message.Claim(Guid.NewGuid(), ReputationEngineTests.Now);
        var outbox = Substitute.For<IOutboxRepository>();
        outbox.ClaimAsync(Arg.Is<IReadOnlyCollection<string>>(k => k.Count == 2 && k.Contains("ReviewInvitationPush") && k.Contains("ReputationUpdatedPush")),
            Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(message);
        var read = Substitute.For<IReputationReadRepository>();
        read.CanDeliverAsync(message.Kind, push.SessionId, push.BookingId, push.RecipientId, push.ExpiresAt, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(allowed);
        var devices = Substitute.For<IDeviceRepository>(); devices.GetActiveTokensAsync(push.RecipientId, Arg.Any<CancellationToken>()).Returns(new[] { "device" });
        var notifications = Substitute.For<INotificationService>();
        notifications.SendMulticastNotificationAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(fail ? 0 : 1);
        var realtime = Substitute.For<IReputationRealtimePublisher>();
        await using var provider = new ServiceCollection().AddSingleton(outbox).AddSingleton(read).AddSingleton(devices)
            .AddSingleton(notifications).AddSingleton(realtime).BuildServiceProvider();
        var worker = new ReputationNotificationWorker(provider.GetRequiredService<IServiceScopeFactory>(), new ReputationEngineTests.Clock(), NullLogger<ReputationNotificationWorker>.Instance);
        Assert.True(await worker.ProcessOnceAsync(default));
        if (allowed && fail)
        {
            await outbox.Received(1).FailAsync(message.Id, message.LeaseId!.Value, Arg.Any<DateTime>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
            await worker.ProcessOnceAsync(default);
            Assert.Equal(JsonSerializer.Serialize(push), message.Payload);
        }
        else await outbox.Received(1).CompleteAsync(message.Id, message.LeaseId!.Value, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        if (allowed && !fail) await realtime.Received(1).PublishAsync(message.Kind, push, Arg.Any<CancellationToken>());
        else await realtime.DidNotReceiveWithAnyArgs().PublishAsync(default!, default!, default);
        if (!allowed) await devices.DidNotReceiveWithAnyArgs().GetActiveTokensAsync(default, default);
    }
    [Fact]
    public async Task PermissionLostAfterPushPreventsRealtime()
    {
        var push = new ReputationPush(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null);
        var message = new OutboxMessage("ReputationUpdatedPush", push.RecipientId, JsonSerializer.Serialize(push), ReputationEngineTests.Now);
        var read = Substitute.For<IReputationReadRepository>();
        read.CanDeliverAsync(message.Kind, push.SessionId, push.BookingId, push.RecipientId, null, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true, true, false);
        var devices = Substitute.For<IDeviceRepository>(); devices.GetActiveTokensAsync(push.RecipientId, Arg.Any<CancellationToken>()).Returns(new[] { "device" });
        var notifications = Substitute.For<INotificationService>();
        notifications.SendMulticastNotificationAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(1);
        var realtime = Substitute.For<IReputationRealtimePublisher>();
        await using var provider = new ServiceCollection().AddSingleton(read).AddSingleton(devices).AddSingleton(notifications).AddSingleton(realtime).BuildServiceProvider();
        var worker = new ReputationNotificationWorker(provider.GetRequiredService<IServiceScopeFactory>(), new ReputationEngineTests.Clock(), NullLogger<ReputationNotificationWorker>.Instance);
        await worker.DeliverAsync(provider, message, default);
        await realtime.DidNotReceiveWithAnyArgs().PublishAsync(default!, default!, default);
    }
}
