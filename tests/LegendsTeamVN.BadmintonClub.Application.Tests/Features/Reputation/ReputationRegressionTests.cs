using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Notifications;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.BadmintonClub.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Reputation;

public sealed class ReputationRegressionTests
{
    [Fact]
    public void RejoiningWaitingListPreservesUnrecoveredLateCancellationThroughAssessment()
    {
        var now = ReputationEngineTests.Now;
        var user = new AppUser(Guid.NewGuid(), "Present player");
        var group = new ClubGroup(groupName: "Regression");
        var session = new BadmintonSession(group.Id, "Regression", now.AddHours(-4), now.AddHours(-2), "Court", courtCount: 1);
        var present = new SessionBooking(session.Id, user.Id);
        present.MarkPresent(user.Id, session.StartTime);
        var cancelled = new SessionBooking(session.Id, Guid.NewGuid(), Guid.NewGuid());
        session.Bookings.Add(present);
        session.Bookings.Add(cancelled);
        cancelled.CancelWithEvidence(session, cancelled.UserId, ReputationCancellationSource.Recruitment, session.StartTime.AddMinutes(-30));
        cancelled.UpdateStatus(BookingStatus.WAITING);
        cancelled.UpdateQueueOrder(1);
        var published = session.PublishSettlement(new GroupFeeRule(group.Id, FeeMode.FIXED, dailyMemberFee: 0),
            [user], [new GroupMember(group.Id, user.Id, Guid.NewGuid(), "DAILY")], new(0, 0, 0, 0), [],
            Guid.NewGuid(), new string('a', 64), user.Id, session.EndTime);
        Assert.True(published.IsSuccess, published.Error.Message);
        session.UpdateStatus(SessionStatus.COMPLETED);
        Assert.True(session.AssessReputation(new(0, 0, 0, 0, 1, 0), session.Fees.ToArray(), [], [], now));
        Assert.Equal(-10m, cancelled.ReputationRawDelta);
        Assert.False(cancelled.ReputationReviewEligible);
        Assert.Equal(cancelled.CancellationHistory[0], cancelled.ReputationEvidence!.Cancellation);
    }

    [Theory]
    [InlineData("partial")]
    [InlineData("exception")]
    [InlineData("timeout")]
    [InlineData("realtime")]
    public async Task ChannelFailureDoesNotBlockOtherChannelAndKeepsOutboxRetry(string failure)
    {
        var push = new ReputationPush(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null);
        var message = new OutboxMessage("ReputationUpdatedPush", push.RecipientId, JsonSerializer.Serialize(push), ReputationEngineTests.Now);
        message.Claim(Guid.NewGuid(), ReputationEngineTests.Now);
        var outbox = Substitute.For<IOutboxRepository>();
        outbox.ClaimAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(message);
        var read = Substitute.For<IReputationReadRepository>();
        read.CanDeliverAsync(message.Kind, push.SessionId, push.BookingId, push.RecipientId, null, Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(true);
        var devices = Substitute.For<IDeviceRepository>();
        devices.GetActiveTokensAsync(push.RecipientId, Arg.Any<CancellationToken>()).Returns(new[] { "valid", "expired" });
        var notifications = Substitute.For<INotificationService>();
        notifications.SendMulticastNotificationAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(_ => failure switch
            {
                "exception" => Task.FromException<int>(new InvalidOperationException("Push unavailable")),
                "timeout" => Task.FromException<int>(new OperationCanceledException()),
                "realtime" => Task.FromResult(2),
                _ => Task.FromResult(1)
            });
        var realtime = Substitute.For<IReputationRealtimePublisher>();
        if (failure == "realtime")
            realtime.PublishAsync(message.Kind, push, Arg.Any<CancellationToken>())
                .Returns(Task.FromException(new InvalidOperationException("Realtime unavailable")));
        await using var services = new ServiceCollection().AddSingleton(outbox).AddSingleton(read).AddSingleton(devices)
            .AddSingleton(notifications).AddSingleton(realtime).BuildServiceProvider();
        var worker = new ReputationNotificationWorker(services.GetRequiredService<IServiceScopeFactory>(), new ReputationEngineTests.Clock(), NullLogger<ReputationNotificationWorker>.Instance);
        Assert.True(await worker.ProcessOnceAsync(default));
        await realtime.Received(1).PublishAsync(message.Kind, push, Arg.Any<CancellationToken>());
        await notifications.Received(1).SendMulticastNotificationAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>());
        await outbox.Received(1).FailAsync(message.Id, message.LeaseId!.Value, Arg.Any<DateTime>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await outbox.DidNotReceiveWithAnyArgs().CompleteAsync(default, default, default, default);
    }
}
