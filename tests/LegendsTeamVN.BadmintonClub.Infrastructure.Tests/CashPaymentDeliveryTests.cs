using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Notifications;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Infrastructure.Payments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Tests;

public sealed class CashPaymentDeliveryTests
{
    [Fact]
    public async Task PartialDeliveryRetriesSameEventAndRequest()
    {
        await using var f = new Fixture();
        var sent = new List<Dictionary<string, string>>();
        f.Notifications.Setup(n => n.SendMulticastNotificationAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<string>, string, string, Dictionary<string, string>, CancellationToken>((_, _, _, data, _) => sent.Add(data))
            .ReturnsAsync(() => sent.Count == 1 ? 0 : 1);
        await f.RunToCompletion();
        Assert.Equal(1, f.Message.Attempts);
        Assert.Equal(2, sent.Count);
        Assert.Equal(sent[0]["eventId"], sent[1]["eventId"]);
        Assert.Equal(f.Push.RequestId.ToString(), sent[0]["requestId"]);
        Assert.Equal(f.Push.FeeId.ToString(), sent[0]["feeId"]);
        Assert.Equal(0, f.UnsupportedClaims);
    }

    [Fact]
    public async Task PaidCancelledOrRevokedRequestCompletesWithoutAccessingDevices()
    {
        await using var f = new Fixture();
        f.Fees.Setup(r => r.IsCashRequestPendingAsync(f.Push.FeeId, f.Push.RequestId, f.Push.RecipientId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        await f.RunToCompletion();
        Assert.NotNull(f.Message.DeliveredAt);
        f.Devices.Verify(d => d.GetActiveTokensAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        f.VerifyNoPush();
    }

    [Fact]
    public async Task NoDevicesCompletesWithoutPush()
    {
        await using var f = new Fixture();
        f.Devices.Setup(d => d.GetActiveTokensAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<string>());
        await f.RunToCompletion();
        f.VerifyNoPush();
    }

    [Fact]
    public async Task DispatchOffDoesNotClaimThenResumesSameMessage()
    {
        await using var f = new Fixture();
        f.Settings.DispatchEnabled = false;
        await f.Worker.StartAsync(default);
        await Task.Delay(1100);
        Assert.Equal(0, f.ClaimCount);
        f.VerifyNoPush();
        f.Settings.DispatchEnabled = true;
        await f.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Worker.StopAsync(default);
        Assert.NotNull(f.Message.DeliveredAt);
    }

    [Fact]
    public async Task PermissionRevokedBetweenBatchesStopsRemainingDevices()
    {
        await using var f = new Fixture();
        f.Devices.Setup(d => d.GetActiveTokensAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, 501).Select(i => "device-" + i).ToArray());
        f.Fees.SetupSequence(r => r.IsCashRequestPendingAsync(f.Push.FeeId, f.Push.RequestId, f.Push.RecipientId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true).ReturnsAsync(true).ReturnsAsync(false);
        f.Notifications.Setup(n => n.SendMulticastNotificationAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(500);
        await f.RunToCompletion();
        f.Notifications.Verify(n => n.SendMulticastNotificationAsync(It.Is<IReadOnlyList<string>>(x => x.Count == 500),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchDisabledAfterFirstBatchKeepsMessageForRetry()
    {
        await using var f = new Fixture();
        f.Devices.Setup(d => d.GetActiveTokensAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, 501).Select(i => "device-" + i).ToArray());
        f.Notifications.Setup(n => n.SendMulticastNotificationAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback(() => f.Settings.DispatchEnabled = false).ReturnsAsync(500);
        await f.Worker.StartAsync(default);
        await f.Failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Worker.StopAsync(default);
        Assert.Null(f.Message.DeliveredAt);
        Assert.Equal(1, f.Message.Attempts);
    }

    [Fact]
    public async Task ProviderFailureReachesDeadLetterWithoutCompleting()
    {
        await using var f = new Fixture();
        f.Notifications.Setup(n => n.SendMulticastNotificationAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("test provider failure"));
        await f.Worker.StartAsync(default);
        await f.DeadLetter.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Worker.StopAsync(default);
        Assert.Equal(10, f.Message.Attempts);
        Assert.Null(f.Message.DeliveredAt);
        Assert.Equal(nameof(InvalidOperationException), f.Message.LastError);
    }

    [Fact]
    public async Task InvalidEnvelopeNeverSendsToAnyDevice()
    {
        await using var f = new Fixture(invalid: true);
        await f.Worker.StartAsync(default);
        await f.DeadLetter.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Worker.StopAsync(default);
        f.VerifyNoPush();
        Assert.Null(f.Message.DeliveredAt);
    }

    [Fact]
    public async Task ProviderThatWaitsForCancellationTimesOutAndLeavesEnvelopeForRetry()
    {
        await using var f = new Fixture();
        f.Notifications.Setup(n => n.SendMulticastNotificationAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Returns(async (IReadOnlyList<string> _, string _, string _, Dictionary<string, string> _, CancellationToken ct) =>
            {
                try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
                catch (OperationCanceledException) { f.Settings.DispatchEnabled = false; throw; }
                return 0;
            });
        await f.Worker.StartAsync(default);
        await f.Failed.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await f.Worker.StopAsync(default);
        Assert.Equal(1, f.Message.Attempts);
        Assert.Null(f.Message.DeliveredAt);
        Assert.Null(f.Message.LeaseId);
        Assert.Equal(nameof(TaskCanceledException), f.Message.LastError);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public CashPaymentRequestedPush Push { get; }
        public OutboxMessage Message { get; }
        public Mock<IPaymentRepository> Fees { get; } = new();
        public Mock<IDeviceRepository> Devices { get; } = new();
        public Mock<INotificationService> Notifications { get; } = new();
        public PaymentsOptions Settings { get; } = new() { Enabled = true, DispatchEnabled = true };
        public CashPaymentNotificationWorker Worker { get; }
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Failed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DeadLetter { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ClaimCount { get; private set; }
        public int UnsupportedClaims { get; private set; }
        private readonly ServiceProvider services;

        public Fixture(bool invalid = false)
        {
            Push = new(Guid.NewGuid(), Guid.NewGuid(), invalid ? Guid.Empty : Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            Message = new("CashPaymentRequestedPush", Push.RecipientId, JsonSerializer.Serialize(Push), DateTime.UtcNow);
            var outbox = new Mock<IOutboxRepository>();
            outbox.Setup(r => r.ClaimAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Returns<IReadOnlyCollection<string>, DateTime, CancellationToken>((kinds, _, _) =>
                {
                    ClaimCount++;
                    if (kinds.Count != 1 || !kinds.Contains("CashPaymentRequestedPush")) { UnsupportedClaims++; return Task.FromResult<OutboxMessage?>(null); }
                    if (Message.DeliveredAt.HasValue || Message.Attempts >= 10) return Task.FromResult<OutboxMessage?>(null);
                    Message.Claim(Guid.NewGuid(), DateTime.UtcNow);
                    return Task.FromResult<OutboxMessage?>(Message);
                });
            outbox.Setup(r => r.CompleteAsync(Message.Id, It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Callback<Guid, Guid, DateTime, CancellationToken>((_, lease, now, _) =>
                {
                    Assert.Equal(Message.LeaseId, lease); Message.Complete(now); Completed.TrySetResult();
                }).Returns(Task.CompletedTask);
            outbox.Setup(r => r.FailAsync(Message.Id, It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<Guid, Guid, DateTime, string, CancellationToken>((_, lease, now, error, _) =>
                {
                    Assert.Equal(Message.LeaseId, lease); Message.Fail(now, error); Failed.TrySetResult();
                    if (Message.Attempts == 10) DeadLetter.TrySetResult();
                }).Returns(Task.CompletedTask);
            Fees.Setup(r => r.IsCashRequestPendingAsync(Push.FeeId, Push.RequestId, Push.RecipientId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Devices.Setup(d => d.GetActiveTokensAsync(Push.RecipientId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { "test-device" });
            Notifications.Setup(n => n.SendMulticastNotificationAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
            services = new ServiceCollection().AddSingleton(outbox.Object).AddSingleton(Fees.Object)
                .AddSingleton(Devices.Object).AddSingleton(Notifications.Object).BuildServiceProvider();
            var options = new Mock<IOptionsMonitor<PaymentsOptions>>();
            options.SetupGet(o => o.CurrentValue).Returns(Settings);
            Worker = new(services.GetRequiredService<IServiceScopeFactory>(), options.Object, TimeProvider.System,
                NullLogger<CashPaymentNotificationWorker>.Instance);
        }

        public async Task RunToCompletion()
        {
            await Worker.StartAsync(default);
            await Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Worker.StopAsync(default);
        }

        public void VerifyNoPush() => Notifications.Verify(n => n.SendMulticastNotificationAsync(It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);

        public async ValueTask DisposeAsync() { Worker.Dispose(); await services.DisposeAsync(); }
    }
}
