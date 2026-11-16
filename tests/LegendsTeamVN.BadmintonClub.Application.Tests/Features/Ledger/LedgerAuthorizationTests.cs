using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Requests;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;
using LegendsTeamVN.BadmintonClub.Application.Features.Ledger;
using LegendsTeamVN.BadmintonClub.Application.Features.Ledger.GetOverview;
using LegendsTeamVN.BadmintonClub.Application.Tests.Common;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.Core.Identity.Abstractions;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Ledger;

public sealed class LedgerAuthorizationTests
{
    [Fact]
    public async Task CachedGuardCannotGrantDatabasePermissionAndDisabledFeatureDoesNotRead()
    {
        var repo = Substitute.For<ILedgerReadRepository>();
        var user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns(Guid.NewGuid());
        var flags = new LedgerOptions { Enabled = true };
        var handler = new GetLedgerOverviewQueryHandler(repo, user, new TestOptionsMonitor<LedgerOptions>(flags));
        var group = Guid.NewGuid();
        var denied = await handler.Handle(new(group, new()), default);
        Assert.Equal("Ledger.Forbidden", denied.Error.Code);
        await repo.DidNotReceive().GetOverviewAsync(Arg.Any<Guid>(), Arg.Any<LedgerReadFilter>(), Arg.Any<CancellationToken>());
        flags.Enabled = false;
        var disabled = await handler.Handle(new(group, new()), default);
        Assert.Equal("Ledger.Disabled", disabled.Error.Code);
        user.UserId.Returns((Guid?)null);
        Assert.Equal("User.Unauthorized", (await handler.Handle(new(group, new()), default)).Error.Code);
    }

    [Fact]
    public async Task CapabilitiesReflectManagePermissionAndWriteFlag()
    {
        var repo = Substitute.For<ILedgerReadRepository>();
        var user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns(Guid.NewGuid());
        repo.HasPermissionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), GroupPermissions.Finance.LedgerView, Arg.Any<CancellationToken>()).Returns(true);
        repo.HasPermissionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), GroupPermissions.Finance.LedgerManage, Arg.Any<CancellationToken>()).Returns(true);
        repo.GetOverviewAsync(Arg.Any<Guid>(), Arg.Any<LedgerReadFilter>(), Arg.Any<CancellationToken>())
            .Returns(new LedgerOverviewResponse(0, 0, 0, 0, 0, 0, DateTime.UtcNow, new(false, false, true)));
        var handler = new GetLedgerOverviewQueryHandler(repo, user, new TestOptionsMonitor<LedgerOptions>(new() { Enabled = true, WritesEnabled = true }));
        var result = await handler.Handle(new(Guid.NewGuid(), new()), default);
        Assert.True(result.Value.Capabilities.CanManage);
        Assert.True(result.Value.Capabilities.CanWrite);
    }

    [Fact]
    public void FiltersRejectOverflowNonUtcInvalidMonthAndUnknownDebtKind()
    {
        var validator = new LedgerReadFilterValidator();
        Assert.False(validator.Validate(new LedgerReadFilter(Page: int.MaxValue)).IsValid);
        Assert.False(validator.Validate(new LedgerReadFilter(FromUtc: DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified))).IsValid);
        Assert.False(validator.Validate(new LedgerReadFilter(ContributionMonth: new DateOnly(2026, 11, 2))).IsValid);
        Assert.False(validator.Validate(new LedgerReadFilter(DebtType: "BANK")).IsValid);
    }
}
