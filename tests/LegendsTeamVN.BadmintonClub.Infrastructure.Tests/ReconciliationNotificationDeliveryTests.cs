using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Notifications;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Infrastructure.Payments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Tests;

public sealed class ReconciliationNotificationDeliveryTests
{
    private sealed class Fixture : IAsyncDisposable
    {
        public Guid Recipient { get; } = Guid.NewGuid();
        public Guid Fee { get; } = Guid.NewGuid();
        public Guid Group { get; } = Guid.NewGuid();
        public Guid Transaction { get; } = Guid.NewGuid();
        public OutboxMessage Message { get; }
        public Mock<IReconciliationRepository> Repository { get; } = new();
        public Mock<IDeviceRepository> Devices { get; } = new();
        public Mock<INotificationService> Notifications { get; } = new();
        public ReconciliationOptions Settings { get; } = new() { DispatchEnabled = true };
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DeadLetter { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ReconciliationNotificationWorker Worker { get; }
        public int ClaimCount { get; private set; }
        private readonly ServiceProvider services;

        public Fixture(bool alert = false, bool invalid = false)
        {
            var payload = alert ? JsonSerializer.Serialize(new ReconciliationAlertPush(Guid.NewGuid(), Transaction, Recipient, Group))
                : JsonSerializer.Serialize(new PaymentConfirmedPush(Guid.NewGuid(), Fee, Recipient, Group, Guid.NewGuid()));
            Message = new(alert ? "ReconciliationAlertPush" : "PaymentConfirmedPush", invalid ? Guid.NewGuid() : Recipient, payload, DateTime.UtcNow);
            var outbox = new Mock<IOutboxRepository>();
            outbox.Setup(r => r.ClaimAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Returns<IReadOnlyCollection<string>, DateTime, CancellationToken>((kinds, _, _) =>
                {
                    Assert.Equal(new[] { "PaymentConfirmedPush", "ReconciliationAlertPush" }, kinds.OrderBy(x => x).ToArray());
                    ClaimCount++;
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
                    Assert.Equal(Message.LeaseId, lease); Message.Fail(now, error);
                    if (Message.Attempts == 10) DeadLetter.TrySetResult();
                }).Returns(Task.CompletedTask);
            Repository.Setup(r => r.CanReceiveConfirmationAsync(Fee, Group, It.IsAny<Guid>(), Recipient, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Repository.Setup(r => r.CanReceiveAlertAsync(Transaction, Group, Recipient, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Devices.Setup(r => r.GetActiveTokensAsync(Recipient, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { "uc18-test-device" });
            Notifications.Setup(r => r.SendMulticastNotificationAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
            services = new ServiceCollection().AddSingleton(outbox.Object).AddSingleton(Repository.Object)
                .AddSingleton(Devices.Object).AddSingleton(Notifications.Object).BuildServiceProvider();
            Worker = new(services.GetRequiredService<IServiceScopeFactory>(), new ReconciliationTestOptions<ReconciliationOptions>(Settings),
                TimeProvider.System, NullLogger<ReconciliationNotificationWorker>.Instance);
        }

        public async Task Run()
        {
            await Worker.StartAsync(default);
            await Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Worker.StopAsync(default);
        }

        public async ValueTask DisposeAsync() { Worker.Dispose(); await services.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BothKindsClaimOnlyUc18AndDeliverAfterRecheckingRecipient(bool alert)
    {
        await using var f = new Fixture(alert);
        await f.Run();
        Assert.NotNull(f.Message.DeliveredAt);
        f.Notifications.Verify(r => r.SendMulticastNotificationAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<string>(),
            It.Is<Dictionary<string, string>>(data => data.ContainsKey("eventId") && data.ContainsKey(alert ? "transactionId" : "feeId")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokedPermissionCompletesAlertWithoutAccessingDevices()
    {
        await using var f = new Fixture(alert: true);
        f.Repository.Setup(r => r.CanReceiveAlertAsync(f.Transaction, f.Group, f.Recipient, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        await f.Run();
        f.Devices.Verify(r => r.GetActiveTokensAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PartialDeliveryRetriesWithStableEventId()
    {
        await using var f = new Fixture();
        var eventIds = new List<string>();
        f.Notifications.Setup(r => r.SendMulticastNotificationAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<string>, string, string, Dictionary<string, string>, CancellationToken>((_, _, _, data, _) => eventIds.Add(data["eventId"]))
            .ReturnsAsync(() => eventIds.Count == 1 ? 0 : 1);
        await f.Run();
        Assert.Equal(1, f.Message.Attempts);
        Assert.Equal(2, eventIds.Count);
        Assert.Single(eventIds.Distinct());
    }

    [Fact]
    public async Task DispatchOffDoesNotClaimAndThenResumes()
    {
        await using var f = new Fixture();
        f.Settings.DispatchEnabled = false;
        await f.Worker.StartAsync(default);
        await Task.Delay(1100);
        Assert.Equal(0, f.ClaimCount);
        f.Settings.DispatchEnabled = true;
        await f.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Worker.StopAsync(default);
    }

    [Fact]
    public async Task InvalidRecipientDeadLettersAtTenAttemptsWithoutSending()
    {
        await using var f = new Fixture(invalid: true);
        await f.Worker.StartAsync(default);
        await f.DeadLetter.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Worker.StopAsync(default);
        Assert.Equal(10, f.Message.Attempts);
        Assert.Null(f.Message.DeliveredAt);
        f.Devices.Verify(r => r.GetActiveTokensAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task PermissionRevokedBetweenBatchesStopsRemainingDevices()
    {
        await using var f = new Fixture(alert: true);
        var allowed = true;
        f.Repository.Setup(r => r.CanReceiveAlertAsync(f.Transaction, f.Group, f.Recipient, It.IsAny<CancellationToken>())).ReturnsAsync(() => allowed);
        f.Devices.Setup(r => r.GetActiveTokensAsync(f.Recipient, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, 501).Select(i => "test-" + i).ToArray());
        f.Notifications.Setup(r => r.SendMulticastNotificationAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback(() => allowed = false).ReturnsAsync(500);
        await f.Run();
        f.Notifications.Verify(r => r.SendMulticastNotificationAsync(It.Is<IReadOnlyList<string>>(tokens => tokens.Count == 500),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
