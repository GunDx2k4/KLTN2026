using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Notifications;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Tests;

public sealed class LedgerNotificationDeliveryTests
{
    [Fact]
    public async Task WorkerClaimsOnlyLedgerRetriesSameEventAndRechecksRevokedPermission()
    {
        var push = new LedgerUpdatedPush(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var message = new OutboxMessage("LedgerUpdatedPush", push.RecipientId, JsonSerializer.Serialize(push), DateTime.UtcNow);
        message.Claim(Guid.NewGuid(), DateTime.UtcNow);
        var flags = new LedgerOptions { Enabled = true, DispatchEnabled = true, RealtimeEnabled = true };
        var options = new Mock<IOptionsMonitor<LedgerOptions>>(); options.SetupGet(o => o.CurrentValue).Returns(flags);
        var outbox = new Mock<IOutboxRepository>();
        outbox.Setup(o => o.ClaimAsync(It.Is<IReadOnlyCollection<string>>(k => k.Count == 1 && k.Contains("LedgerUpdatedPush")), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(message);
        var reads = new Mock<ILedgerReadRepository>();
        reads.Setup(r => r.HasLedgerAsync(push.GroupId, push.LedgerId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        reads.Setup(r => r.HasPermissionAsync(push.GroupId, push.RecipientId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var devices = new Mock<IDeviceRepository>(); devices.Setup(d => d.GetActiveTokensAsync(push.RecipientId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { "token", "token" });
        var notifications = new Mock<INotificationService>();
        notifications.Setup(n => n.SendMulticastNotificationAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException());
        var realtime = new Mock<ILedgerRealtimePublisher>();
        var services = new ServiceCollection().AddSingleton(outbox.Object).AddSingleton(reads.Object).AddSingleton(devices.Object)
            .AddSingleton(notifications.Object).AddSingleton(realtime.Object);
        using var provider = services.BuildServiceProvider();
        var worker = new LedgerNotificationWorker(provider.GetRequiredService<IServiceScopeFactory>(), options.Object, TimeProvider.System, NullLogger<LedgerNotificationWorker>.Instance);
        flags.DispatchEnabled = false;
        Assert.False(await worker.ProcessOnceAsync(default));
        outbox.Verify(o => o.ClaimAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        flags.DispatchEnabled = true;
        await worker.ProcessOnceAsync(default);
        outbox.Verify(o => o.FailAsync(message.Id, message.LeaseId!.Value, It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        reads.Setup(r => r.HasPermissionAsync(push.GroupId, push.RecipientId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        await worker.ProcessOnceAsync(default);
        outbox.Verify(o => o.CompleteAsync(message.Id, message.LeaseId!.Value, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        realtime.Verify(r => r.PublishAsync(It.IsAny<LedgerUpdatedPush>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(push.EventId, JsonSerializer.Deserialize<LedgerUpdatedPush>(message.Payload)!.EventId);
        reads.Setup(r => r.HasPermissionAsync(push.GroupId, push.RecipientId, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        notifications.Setup(n => n.SendMulticastNotificationAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        flags.RealtimeEnabled = false;
        await worker.DeliverAsync(provider, message, default);
        realtime.Verify(r => r.PublishAsync(It.IsAny<LedgerUpdatedPush>(), It.IsAny<CancellationToken>()), Times.Never);
        flags.RealtimeEnabled = true;
        await worker.DeliverAsync(provider, message, default);
        realtime.Verify(r => r.PublishAsync(push, It.IsAny<CancellationToken>()), Times.Once);
        realtime.Setup(r => r.PublishAsync(push, It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException());
        await worker.ProcessOnceAsync(default);
        outbox.Verify(o => o.FailAsync(message.Id, message.LeaseId!.Value, It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        flags.Enabled = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => worker.DeliverAsync(provider, message, default));
    }
}
