using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Infrastructure.Realtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Tests;

public sealed class RotationDeliveryTests
{
    [Theory]
    [InlineData("RotationChanged")]
    [InlineData("InvitationRealtime")]
    [InlineData("InvitationPush")]
    public async Task SuccessfulDeliveryCompletesOwnedMessage(string kind)
    {
        await using var f = new Fixture(kind);
        await f.Worker.StartAsync(default);
        await f.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Worker.StopAsync(default);
        Assert.NotNull(f.Message.DeliveredAt);
        Assert.Equal(0, f.Message.Attempts);
        if (kind == "InvitationPush")
            f.Notifications.Verify(n => n.SendMulticastNotificationAsync(It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TransientPushFailureIsRetriedWithoutRepeatingTheBusinessCommand()
    {
        await using var f = new Fixture("InvitationPush");
        f.Notifications.SetupSequence(n => n.SendMulticastNotificationAsync(It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("test failure")).ReturnsAsync(1);
        await f.Worker.StartAsync(default);
        await f.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Worker.StopAsync(default);
        Assert.Equal(1, f.Message.Attempts);
        Assert.NotNull(f.Message.DeliveredAt);
    }

    [Fact]
    public async Task TenFailuresDeadLetterWithoutMarkingDeliverySuccessful()
    {
        await using var f = new Fixture("InvitationPush");
        f.Notifications.Setup(n => n.SendMulticastNotificationAsync(It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("test failure"));
        await f.Worker.StartAsync(default);
        await f.DeadLetter.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Worker.StopAsync(default);
        Assert.Equal(10, f.Message.Attempts);
        Assert.Null(f.Message.DeliveredAt);
        Assert.NotNull(f.Message.LastError);
    }

    [Fact]
    public async Task InvitationsForFinishedGamesAreDiscardedWithoutSending()
    {
        await using var f = new Fixture("InvitationPush");
        f.Sessions.Setup(s => s.CanReceiveMatchInvitationAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(false);
        await f.Worker.StartAsync(default);
        await f.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await f.Worker.StopAsync(default);
        f.Notifications.Verify(n => n.SendMulticastNotificationAsync(It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public OutboxMessage Message { get; }
        public Mock<INotificationService> Notifications { get; } = new();
        public Mock<ISessionRepository> Sessions { get; } = new();
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DeadLetter { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public RotationOutboxWorker Worker { get; }
        private readonly ServiceProvider provider;

        public Fixture(string kind)
        {
            var now = DateTime.UtcNow;
            var change = new RotationChangedDomainEvent(Guid.NewGuid(), 1, now, Guid.NewGuid(), 1, [Guid.NewGuid()]);
            Message = new OutboxMessage(kind, change.InvitedUserIds[0], JsonSerializer.Serialize(change), now);
            var repository = new Mock<IOutboxRepository>();
            repository.Setup(r => r.ClaimAsync(It.Is<IReadOnlyCollection<string>>(k =>
                k.Count == 3 && k.Contains("RotationChanged") && k.Contains("InvitationRealtime") && k.Contains("InvitationPush")),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).Returns(() =>
            {
                // Timing/backoff is tested separately against PostgreSQL; this fake makes retries deterministic.
                if (Message.DeliveredAt.HasValue || Message.Attempts >= 10) return Task.FromResult<OutboxMessage?>(null);
                Message.Claim(Guid.NewGuid(), DateTime.UtcNow);
                return Task.FromResult<OutboxMessage?>(Message);
            });
            repository.Setup(r => r.CompleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .Callback<Guid, Guid, DateTime, CancellationToken>((_, _, time, _) => { Message.Complete(time); Completed.TrySetResult(); })
                .Returns(Task.CompletedTask);
            repository.Setup(r => r.FailAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<Guid, Guid, DateTime, string, CancellationToken>((_, _, time, error, _) =>
                { Message.Fail(time, error); if (Message.Attempts == 10) DeadLetter.TrySetResult(); }).Returns(Task.CompletedTask);
            Sessions.Setup(s => s.CanReceiveMatchInvitationAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var devices = new Mock<IDeviceRepository>();
            devices.Setup(d => d.GetActiveTokensAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { "fake-device" });
            Notifications.Setup(n => n.SendMulticastNotificationAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
            var realtime = new Mock<ISessionRealtimePublisher>();
            realtime.Setup(r => r.RotationChangedAsync(It.IsAny<Guid>(), It.IsAny<RotationChangedDomainEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            realtime.Setup(r => r.InviteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<RotationChangedDomainEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            provider = new ServiceCollection().AddSingleton(repository.Object).AddSingleton(Sessions.Object)
                .AddSingleton(devices.Object).AddSingleton(Notifications.Object).AddSingleton(realtime.Object).BuildServiceProvider();
            var options = new Mock<IOptionsMonitor<SessionRotationOptions>>();
            options.SetupGet(o => o.CurrentValue).Returns(new SessionRotationOptions { Enabled = true, DispatchEnabled = true });
            Worker = new(provider.GetRequiredService<IServiceScopeFactory>(), options.Object, TimeProvider.System, NullLogger<RotationOutboxWorker>.Instance);
        }

        public async ValueTask DisposeAsync() { Worker.Dispose(); await provider.DisposeAsync(); }
    }
}
