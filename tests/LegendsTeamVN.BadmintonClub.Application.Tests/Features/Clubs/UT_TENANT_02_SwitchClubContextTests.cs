using System.Linq.Expressions;
using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.SwitchContext;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Clubs;

public class UT_TENANT_02_SwitchClubContextTests
{
    private readonly IClubMemberRepository _clubMemberRepository = Substitute.For<IClubMemberRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserManagerService _userManagerService = Substitute.For<IUserManagerService>();
    private readonly IJwtTokenService _jwtTokenService = Substitute.For<IJwtTokenService>();
    private readonly IGroupAuthorizationService _groupAuthService = Substitute.For<IGroupAuthorizationService>();
    private readonly SwitchClubContextCommandHandler _handler;

    public UT_TENANT_02_SwitchClubContextTests()
    {
        _handler = new SwitchClubContextCommandHandler(
            _clubMemberRepository,
            _currentUserService,
            _userManagerService,
            _jwtTokenService,
            _groupAuthService);
    }

    [Fact]
    public async Task Handle_WhenUserIsActiveMember_ShouldReturnSwitchClubResponseWithTokensAndPermissions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        _currentUserService.UserId.Returns(userId);
        _currentUserService.UserName.Returns("host_user");

        var club = new ClubGroup(groupName: "Hội Cầu Lông Hà Nội", description: "CLB Cầu Lông", code: "HCLHN");
        typeof(ClubGroup).GetProperty("Id")?.SetValue(club, clubId);

        var hostRole = new GroupRole(clubId, "Host");
        typeof(GroupRole).GetProperty("Id")?.SetValue(hostRole, roleId);

        var membership = new GroupMember(clubId, userId, roleId, status: ClubMemberStatus.ACTIVE);
        typeof(GroupMember).GetProperty("Group")?.SetValue(membership, club);
        typeof(GroupMember).GetProperty("RoleEntity")?.SetValue(membership, hostRole);

        _clubMemberRepository.FindSingleAsync(
            Arg.Any<Expression<Func<GroupMember, bool>>>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<Expression<Func<GroupMember, object>>[]>())
            .Returns(membership);

        var hostPermissions = GroupPermissions.All.Select(p => p.Code).ToList();
        _groupAuthService.GetUserPermissionsInGroupAsync(userId, clubId, Arg.Any<CancellationToken>())
            .Returns(hostPermissions);

        _userManagerService.GetUserEmailAsync(userId)
            .Returns("host@example.com");

        _jwtTokenService.GenerateAccessToken(
            userId: userId,
            email: "host@example.com",
            userName: "host_user",
            roles: Arg.Is<List<string>>(r => r.Contains("Host")),
            tenantId: clubId,
            permissions: Arg.Any<List<string>>())
            .Returns("switched_tenant_token");

        var command = new SwitchClubContextCommand(clubId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.ClubId.Should().Be(clubId);
        result.Value.ClubName.Should().Be("Hội Cầu Lông Hà Nội");
        result.Value.RoleName.Should().Be("Host");
        result.Value.AccessToken.Should().Be("switched_tenant_token");
        result.Value.Permissions.Should().Contain(GroupPermissions.Operations.SessionCreate);
        result.Value.Permissions.Should().Contain(GroupPermissions.Finance.LedgerView);
    }

    [Fact]
    public async Task Handle_WhenUserIsNotMemberOfTargetClub_ShouldReturnNotFoundError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var targetClubId = Guid.NewGuid();

        _currentUserService.UserId.Returns(userId);
        _clubMemberRepository.FindSingleAsync(
            Arg.Any<Expression<Func<GroupMember, bool>>>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<Expression<Func<GroupMember, object>>[]>())
            .Returns((GroupMember?)null);

        var command = new SwitchClubContextCommand(targetClubId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ClubMember.NotFound");
    }
}
