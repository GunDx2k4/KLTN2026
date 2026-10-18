using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.ProcessDue;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Infrastructure.Realtime;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Tests;

public sealed class RotationSchedulingTests
{
    [Fact]
    public async Task RecoveryAndDueAreDeduplicatedAndDoNotDependOnDeliveryFlag()
    {
        var id = Guid.NewGuid(); var sessions = Repository();
        sessions.Setup(s => s.FindDueRotationIdsAsync(It.IsAny<DateTime>(), 32, It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .ReturnsAsync(new[] { id });
        sessions.Setup(s => s.FindRotationRecoveryIdsAsync(It.IsAny<DateTime>(), null, 32, default)).ReturnsAsync(new[] { id });
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<ProcessDueRotationCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        using var provider = new ServiceCollection().AddScoped(_ => sessions.Object).AddScoped(_ => sender.Object).BuildServiceProvider();
        var settings = new SessionRotationOptions { Enabled = true, DispatchEnabled = false };
        using var worker = Worker(provider, settings);
        await worker.RunOnceAsync(default);
        sender.Verify(s => s.Send(It.Is<ProcessDueRotationCommand>(c => c.SessionId == id && c.Recover), It.IsAny<CancellationToken>()), Times.Once);
        settings.Enabled = false; await worker.RunOnceAsync(default);
        sender.Verify(s => s.Send(It.IsAny<ProcessDueRotationCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FailureDoesNotBlockAnotherSessionAndIsBackedOff()
    {
        var broken = Guid.NewGuid(); var good = Guid.NewGuid(); var sessions = Repository();
        sessions.Setup(s => s.FindDueRotationIdsAsync(It.IsAny<DateTime>(), 32, It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .ReturnsAsync(new[] { broken, good });
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.Is<ProcessDueRotationCommand>(c => c.SessionId == broken), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException());
        sender.Setup(s => s.Send(It.Is<ProcessDueRotationCommand>(c => c.SessionId == good), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        using var provider = new ServiceCollection().AddScoped(_ => sessions.Object).AddScoped(_ => sender.Object).BuildServiceProvider();
        using var worker = Worker(provider, new SessionRotationOptions { Enabled = true });
        await worker.RunOnceAsync(default); await worker.RunOnceAsync(default);
        sender.Verify(s => s.Send(It.Is<ProcessDueRotationCommand>(c => c.SessionId == broken), It.IsAny<CancellationToken>()), Times.Once);
        sender.Verify(s => s.Send(It.Is<ProcessDueRotationCommand>(c => c.SessionId == good), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private static Mock<ISessionRepository> Repository()
    {
        var mock = new Mock<ISessionRepository>();
        mock.Setup(s => s.FindDueRotationIdsAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .ReturnsAsync(Array.Empty<Guid>());
        mock.Setup(s => s.FindRotationRecoveryIdsAsync(It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<int>(), default))
            .ReturnsAsync(Array.Empty<Guid>());
        return mock;
    }

    private static RotationSchedulingWorker Worker(ServiceProvider provider, SessionRotationOptions settings)
    {
        var options = new Mock<IOptionsMonitor<SessionRotationOptions>>(); options.SetupGet(o => o.CurrentValue).Returns(settings);
        return new(provider.GetRequiredService<IServiceScopeFactory>(), options.Object, TimeProvider.System,
            NullLogger<RotationSchedulingWorker>.Instance);
    }
}
