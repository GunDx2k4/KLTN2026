using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.UpdateMemberFeePlan;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Identity.Abstractions;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Clubs;

public sealed class UpdateMemberFeePlanTests
{
    [Fact]
    public async Task ChangeToMonthlyRecordsStartWithoutGrantingPaidTime()
    {
        var club = Guid.NewGuid();
        var user = Guid.NewGuid();
        var member = new GroupMember(club, user, Guid.NewGuid(), "DAILY", null, ClubMemberStatus.ACTIVE, "Test");
        var repository = Substitute.For<ILedgerWriteRepository>();
        var authorization = Substitute.For<IGroupAuthorizationService>();
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(user);
        authorization.HasPermissionAsync(user, club, GroupPermissions.Member.RoleManage, Arg.Any<CancellationToken>()).Returns(true);
        repository.LockGroupAsync(club, Arg.Any<CancellationToken>()).Returns(true);
        repository.LockMemberAsync(club, user, Arg.Any<CancellationToken>()).Returns(member);

        var result = await new UpdateMemberFeePlanCommandHandler(repository, authorization, currentUser)
            .Handle(new(club, user, "MONTHLY"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("MONTHLY", member.MemberType);
        Assert.NotNull(member.MonthlyMembershipSince);
        Assert.Null(member.MonthlyExpiryDate);
    }

    [Fact]
    public async Task UnpaidContributionPreventsChangingPlan()
    {
        var club = Guid.NewGuid();
        var user = Guid.NewGuid();
        var member = new GroupMember(club, user, Guid.NewGuid(), "MONTHLY", DateTime.UtcNow.AddDays(10), ClubMemberStatus.ACTIVE, "Test");
        var repository = Substitute.For<ILedgerWriteRepository>();
        var authorization = Substitute.For<IGroupAuthorizationService>();
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(user);
        authorization.HasPermissionAsync(user, club, GroupPermissions.Member.RoleManage, Arg.Any<CancellationToken>()).Returns(true);
        repository.LockGroupAsync(club, Arg.Any<CancellationToken>()).Returns(true);
        repository.LockMemberAsync(club, user, Arg.Any<CancellationToken>()).Returns(member);
        repository.HasUnpaidContributionAsync(club, user, Arg.Any<CancellationToken>()).Returns(true);

        var result = await new UpdateMemberFeePlanCommandHandler(repository, authorization, currentUser)
            .Handle(new(club, user, "DAILY"), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Club.UnpaidMonthlyContribution", result.Error.Code);
        Assert.Equal("MONTHLY", member.MemberType);
    }
}
