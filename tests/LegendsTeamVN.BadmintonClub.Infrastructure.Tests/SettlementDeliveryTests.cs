using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Settlement.Events;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Tests;

public sealed class SettlementDeliveryTests
{
    [Fact]
    public async Task PartialPushRetriesSameEventWithoutCreatingNewFees()
    {
        await using var f = new Fixture();
        var calls = new List<Dictionary<string, string>>();
        f.Notifications.Setup(n => n.SendMulticastNotificationAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<string>, string, string, Dictionary<string, string>, CancellationToken>((_, _, _, data, _) => calls.Add(data))
            .ReturnsAsync(() => calls.Count == 1 ? 0 : 1);
        await f.Run();
        Assert.Equal(1, f.Message.Attempts);
        Assert.Equal(2, calls.Count);
        Assert.Equal(calls[0]["eventId"], calls[1]["eventId"]);
        Assert.Equal(f.Fee.Id.ToString(), calls[0]["feeId"]);
        Assert.Equal("10", calls[0]["amount"]);
        Assert.Single(f.Session.Fees);
        Assert.Equal(PaymentStatus.UNPAID, f.Fee.PaymentStatus);
    }

    [Fact]
    public async Task PaidFeeIsCompletedWithoutSendingPaymentReminder()
    {
        await using var f = new Fixture();
        f.Fee.MarkAsPaid(PaymentMethod.CASH);
        await f.Run();
        Assert.NotNull(f.Message.DeliveredAt);
        f.VerifyNoPush();
    }

    [Fact]
    public async Task NoDevicesCompletesDeliveryWithoutPush()
    {
        await using var f = new Fixture();
        f.Devices.Setup(d => d.GetActiveTokensAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<string>());
        await f.Run(); f.VerifyNoPush();
    }

    [Fact]
    public async Task DisabledDispatchLeavesMessagePendingThenResumesWithoutCreatingAnotherFee()
    {
        await using var f = new Fixture();
        f.Settings.DispatchEnabled = false;
        await f.Worker.StartAsync(default);
        await Task.Delay(1100);
        Assert.Equal(0, f.ClaimCount);
        Assert.Null(f.Message.DeliveredAt);
        Assert.Null(f.Message.LeaseId);
        f.VerifyNoPush();
        f.Settings.DispatchEnabled = true;
        await f.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Worker.StopAsync(default);
        Assert.Single(f.Session.Fees);
        Assert.NotNull(f.Message.DeliveredAt);
        Assert.Equal(0, f.Message.Attempts);
    }

    [Fact]
    public async Task FailedDeliveriesReachDeadLetterAndNeverComplete()
    {
        await using var f = new Fixture();
        f.Notifications.Setup(n => n.SendMulticastNotificationAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("fake"));
        await f.Worker.StartAsync(default);
        await f.DeadLetter.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Worker.StopAsync(default);
        Assert.Equal(10, f.Message.Attempts);
        Assert.Null(f.Message.DeliveredAt);
        Assert.Equal(nameof(InvalidOperationException), f.Message.LastError);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public BadmintonSession Session { get; }
        public SessionFee Fee => Session.Fees.Single();
        public OutboxMessage Message { get; }
        public Mock<INotificationService> Notifications { get; } = new();
        public Mock<IDeviceRepository> Devices { get; } = new();
        public SettlementNotificationWorker Worker { get; }
        public SettlementOptions Settings { get; } = new() { DispatchEnabled = true };
        public int ClaimCount { get; private set; }
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DeadLetter { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ServiceProvider provider;

        public Fixture()
        {
            var now = DateTime.UtcNow;
            var actor = Guid.NewGuid(); var user = new AppUser(Guid.NewGuid(), "Người chơi");
            Session = new(Guid.NewGuid(), "Buổi chơi", now.AddHours(-2), now, "Sân", courtCount: 1);
            Session.Bookings.Add(new(Session.Id, user.Id, status: BookingStatus.CHECKED_IN));
            var result = Session.PublishSettlement(new GroupFeeRule(Session.GroupId, roundRule: 1), [user], [], new(0, 0, 10, 0), [],
                Guid.NewGuid(), new string('a', 64), actor, now);
            Assert.True(result.IsSuccess, result.Error.Message);
            var payment = new SettlementPaymentPush(Guid.NewGuid(), Session.Id, Fee.Id, user.Id, Fee.Amount, Fee.PaymentCode);
            Message = new("SettlementPaymentPush", user.Id, JsonSerializer.Serialize(payment), now);
            var outbox = new Mock<IOutboxRepository>();
            outbox.Setup(o => o.ClaimAsync(It.Is<IReadOnlyCollection<string>>(k => k.Count == 1 && k.Contains("SettlementPaymentPush")),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).Returns(() =>
                {
                    ClaimCount++;
                    if (Message.DeliveredAt.HasValue || Message.Attempts >= 10) return Task.FromResult<OutboxMessage?>(null);
                    Message.Claim(Guid.NewGuid(), DateTime.UtcNow);
                    return Task.FromResult<OutboxMessage?>(Message);
                });
            outbox.Setup(o => o.CompleteAsync(Message.Id, It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Callback<Guid, Guid, DateTime, CancellationToken>((_, _, time, _) => { Message.Complete(time); Completed.TrySetResult(); })
                .Returns(Task.CompletedTask);
            outbox.Setup(o => o.FailAsync(Message.Id, It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<Guid, Guid, DateTime, string, CancellationToken>((_, _, time, error, _) =>
                { Message.Fail(time, error); if (Message.Attempts == 10) DeadLetter.TrySetResult(); }).Returns(Task.CompletedTask);
            var sessions = new Mock<ISessionRepository>();
            sessions.Setup(s => s.GetSettlementAsync(Session.Id, false, It.IsAny<CancellationToken>())).ReturnsAsync(Session);
            Devices.Setup(d => d.GetActiveTokensAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { "fake-device" });
            Notifications.Setup(n => n.SendMulticastNotificationAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
            provider = new ServiceCollection().AddSingleton(outbox.Object).AddSingleton(sessions.Object)
                .AddSingleton(Devices.Object).AddSingleton(Notifications.Object).BuildServiceProvider();
            var options = new Mock<IOptionsMonitor<SettlementOptions>>();
            options.SetupGet(o => o.CurrentValue).Returns(() => Settings);
            Worker = new(provider.GetRequiredService<IServiceScopeFactory>(), options.Object, TimeProvider.System,
                NullLogger<SettlementNotificationWorker>.Instance);
        }

        public async Task Run()
        {
            await Worker.StartAsync(default);
            await Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Worker.StopAsync(default);
        }

        public void VerifyNoPush() => Notifications.Verify(n => n.SendMulticastNotificationAsync(It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);

        public async ValueTask DisposeAsync() { Worker.Dispose(); await provider.DisposeAsync(); }
    }
}
