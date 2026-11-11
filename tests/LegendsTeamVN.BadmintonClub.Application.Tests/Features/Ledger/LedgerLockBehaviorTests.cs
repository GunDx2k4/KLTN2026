using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.Features.Ledger;
using LegendsTeamVN.BadmintonClub.Application.Features.Ledger.IssueMonthlyContribution;
using LegendsTeamVN.BadmintonClub.Application.Tests.Common;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Ledger;

public sealed class LedgerLockBehaviorTests
{
    [Fact]
    public async Task UnavailableLockOrContentionNeverStartsUnitOfWorkDelegate()
    {
        var locks = Substitute.For<ILedgerLock>();
        var current = Substitute.For<ICurrentUserService>(); current.UserId.Returns(Guid.NewGuid());
        var flags = new LedgerOptions { Enabled = true, WritesEnabled = true };
        var behavior = new LedgerLockBehavior<IssueMonthlyContributionCommand>(locks, current, new TestOptionsMonitor<LedgerOptions>(flags));
        var invoked = false;
        Task<Result<LedgerWriteResponse>> Next(CancellationToken ct) { invoked = true; throw new InvalidOperationException(); }
        var command = new IssueMonthlyContributionCommand(Guid.NewGuid(), Guid.NewGuid(), new(2026, 11, 1));
        locks.AcquireAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<IAsyncDisposable?>(new InvalidOperationException("Redis offline")));
        Assert.Equal("Ledger.LockUnavailable", (await behavior.Handle(command, Next, default)).Error.Code);
        locks.AcquireAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((IAsyncDisposable?)null);
        Assert.Equal("Ledger.Busy", (await behavior.Handle(command, Next, default)).Error.Code);
        flags.WritesEnabled = false;
        Assert.Equal("Ledger.WritesDisabled", (await behavior.Handle(command, Next, default)).Error.Code);
        Assert.False(invoked);
    }
}
