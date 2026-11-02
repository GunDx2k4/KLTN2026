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
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;

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

    [Fact]
    public async Task PersonalInvalidationRechecksEachRecipientAndKeepsHostDelivery()
    {
        await using var f = new Fixture("RotationChanged", true);
        var revoked = Guid.NewGuid(); var allowed = Guid.NewGuid();
        f.Sessions.Setup(s => s.FindPersonalScheduleRecipientsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { revoked, allowed });
        f.Sessions.Setup(s => s.CanAccessPersonalScheduleAsync(It.IsAny<Guid>(), allowed, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await f.Run();
        f.Realtime.Verify(r => r.MyMatchesChangedAsync(It.IsAny<Guid>(), allowed, It.IsAny<RotationChangedDomainEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        f.Realtime.Verify(r => r.MyMatchesChangedAsync(It.IsAny<Guid>(), revoked, It.IsAny<RotationChangedDomainEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Realtime.Verify(r => r.RotationChangedAsync(It.IsAny<Guid>(), It.IsAny<RotationChangedDomainEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PartialPushFailureRetriesTheSameEventWithPersonalRoster()
    {
        await using var f = new Fixture("InvitationPush", true);
        var calls = new List<Dictionary<string, string>>();
        f.Devices.Setup(d => d.GetActiveTokensAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { "fake-device", "fake-device-2" });
        f.Notifications.Setup(n => n.SendMulticastNotificationAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<string>, string, string, Dictionary<string, string>, CancellationToken>((_, _, _, data, _) => calls.Add(data))
            .ReturnsAsync(() => calls.Count == 1 ? 1 : 2);
        await f.Run();
        Assert.Equal(1, f.Message.Attempts); Assert.Equal(2, calls.Count);
        Assert.Equal(calls[0]["eventId"], calls[1]["eventId"]);
        using var teammate = JsonDocument.Parse(calls[0]["teammate"]);
        using var opponents = JsonDocument.Parse(calls[0]["opponents"]);
        Assert.Equal(f.Roster.Players[1].UserId.ToString(), teammate.RootElement.GetProperty("userId").GetString());
        Assert.Equal(2, opponents.RootElement.GetArrayLength()); Assert.Contains("rotationVersion", calls[0].Keys);
    }

    [Fact]
    public async Task PersonalRealtimeUsesEnrichedDtoAndNoLegacyBroadcast()
    {
        await using var f = new Fixture("InvitationRealtime", true); await f.Run();
        f.Realtime.Verify(r => r.PersonalInviteAsync(f.Roster.Players[0].UserId,
            It.Is<MatchInvitationResponse>(i => i.Teammate.UserId == f.Roster.Players[1].UserId && i.Opponents.Count == 2), It.IsAny<CancellationToken>()), Times.Once);
        f.Realtime.Verify(r => r.InviteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<RotationChangedDomainEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("InvitationRealtime")]
    [InlineData("InvitationPush")]
    public async Task RevocationBetweenEligibilityAndRosterReadDiscardsDelivery(string kind)
    {
        await using var f = new Fixture(kind, true);
        f.Sessions.SetupSequence(s => s.CanReceiveMatchInvitationAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true).ReturnsAsync(false);
        await f.Run();
        f.Realtime.Verify(r => r.PersonalInviteAsync(It.IsAny<Guid>(), It.IsAny<MatchInvitationResponse>(), It.IsAny<CancellationToken>()), Times.Never);
        f.Notifications.Verify(n => n.SendMulticastNotificationAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NoDeviceTokenCompletesWithoutPushOrBusinessFailure()
    {
        await using var f = new Fixture("InvitationPush", true);
        f.Devices.Setup(d => d.GetActiveTokensAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<string>());
        await f.Run(); Assert.NotNull(f.Message.DeliveredAt);
        f.Notifications.Verify(n => n.SendMulticastNotificationAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public OutboxMessage Message { get; }
        public Mock<INotificationService> Notifications { get; } = new();
        public Mock<ISessionRepository> Sessions { get; } = new();
        public Mock<IDeviceRepository> Devices { get; } = new();
        public Mock<ISessionRealtimePublisher> Realtime { get; } = new();
        public MatchInvitationRoster Roster { get; }
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DeadLetter { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public RotationOutboxWorker Worker { get; }
        private readonly ServiceProvider provider;

        public Fixture(string kind, bool personal = false)
        {
            var now = DateTime.UtcNow;
            var change = new RotationChangedDomainEvent(Guid.NewGuid(), 1, now, Guid.NewGuid(), 1, [Guid.NewGuid()]);
            Roster = new(change.SessionId, change.StartedMatchId!.Value, 1, now,
                [new(change.InvitedUserIds[0], "Self", SideTeam.TEAM_A), new(Guid.NewGuid(), "Partner", SideTeam.TEAM_A),
                 new(Guid.NewGuid(), "Opponent 1", SideTeam.TEAM_B), new(Guid.NewGuid(), "Opponent 2", SideTeam.TEAM_B)]);
            Sessions.Setup(s => s.GetMatchInvitationRosterAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), true, It.IsAny<CancellationToken>())).ReturnsAsync(Roster);
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
            var devices = Devices;
            devices.Setup(d => d.GetActiveTokensAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { "fake-device" });
            Notifications.Setup(n => n.SendMulticastNotificationAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
            var realtime = Realtime;
            realtime.Setup(r => r.RotationChangedAsync(It.IsAny<Guid>(), It.IsAny<RotationChangedDomainEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            realtime.Setup(r => r.InviteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<RotationChangedDomainEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            provider = new ServiceCollection().AddSingleton(repository.Object).AddSingleton(Sessions.Object)
                .AddSingleton(devices.Object).AddSingleton(Notifications.Object).AddSingleton(realtime.Object).BuildServiceProvider();
            var options = new Mock<IOptionsMonitor<SessionRotationOptions>>();
            options.SetupGet(o => o.CurrentValue).Returns(new SessionRotationOptions { Enabled = true, DispatchEnabled = true, PersonalScheduleEnabled = personal });
            Worker = new(provider.GetRequiredService<IServiceScopeFactory>(), options.Object, TimeProvider.System, NullLogger<RotationOutboxWorker>.Instance);
        }

        public async Task Run()
        {
            await Worker.StartAsync(default); await Completed.Task.WaitAsync(TimeSpan.FromSeconds(5)); await Worker.StopAsync(default);
        }

        public async ValueTask DisposeAsync() { Worker.Dispose(); await provider.DisposeAsync(); }
    }
}
