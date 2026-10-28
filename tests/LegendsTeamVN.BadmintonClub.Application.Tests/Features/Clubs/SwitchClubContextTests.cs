using System.Linq.Expressions;
using FluentAssertions;
using FluentValidation.TestHelper;
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

/// <summary>
/// BỘ KIỂM THỬ ĐƠN VỊ (UNIT TESTS) - FR04 / UC05: Chuyển đổi ngữ cảnh nhóm (Switch Club Context)
/// Mục tiêu: Kiểm thử cô lập từng thành phần: Command Validator và Command Handler logic với Mock dependencies.
/// </summary>
public class SwitchClubContextTests
{
    private readonly IClubMemberRepository _clubMemberRepository = Substitute.For<IClubMemberRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserManagerService _userManagerService = Substitute.For<IUserManagerService>();
    private readonly IJwtTokenService _jwtTokenService = Substitute.For<IJwtTokenService>();
    private readonly IGroupAuthorizationService _groupAuthService = Substitute.For<IGroupAuthorizationService>();
    private readonly SwitchClubContextCommandHandler _handler;
    private readonly SwitchClubContextCommandValidator _validator = new();

    public SwitchClubContextTests()
    {
        _handler = new SwitchClubContextCommandHandler(
            _clubMemberRepository,
            _currentUserService,
            _userManagerService,
            _jwtTokenService,
            _groupAuthService);
    }

    // =========================================================================
    // 1. UNIT TESTS: VALIDATION RULES (Kiểm thử quy tắc dữ liệu đầu vào)
    // =========================================================================

    [Fact]
    [Trait("Category", "UnitTest")]
    public void Validator_WhenClubIdIsEmpty_ShouldHaveValidationError()
    {
        // Arrange
        var command = new SwitchClubContextCommand(Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ClubId)
            .WithErrorMessage("Mã câu lạc bộ không được để trống.");
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public void Validator_WhenClubIdIsValid_ShouldNotHaveValidationError()
    {
        // Arrange
        var command = new SwitchClubContextCommand(Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.ClubId);
    }

    // =========================================================================
    // 2. UNIT TESTS: HANDLER BUSINESS LOGIC (Kiểm thử logic nghiệp vụ xử lý lệnh)
    // =========================================================================

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Handle_WhenUserNotAuthenticated_ShouldReturnUnauthorizedError()
    {
        // Arrange: Người dùng chưa đăng nhập (UserId null)
        _currentUserService.UserId.Returns((Guid?)null);
        var command = new SwitchClubContextCommand(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.Unauthorized");
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Handle_WhenUserIsNotMemberOfTargetClub_ShouldReturnNotFoundError()
    {
        // Arrange: Người dùng không có bản ghi membership trong nhóm
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

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Handle_WhenClubIsInactive_ShouldReturnClubInactiveError()
    {
        // Arrange: CLB bị tạm ngưng hoạt động (IsActive = false)
        var userId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        _currentUserService.UserId.Returns(userId);

        var club = new ClubGroup(groupName: "Hội Cầu Lông Hà Nội", description: "CLB Cầu Lông", code: "HCLHN", isActive: false);
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

        var command = new SwitchClubContextCommand(clubId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Club.Inactive");
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Handle_WhenMemberStatusIsNotActive_ShouldReturnForbiddenError()
    {
        // Arrange: Thành viên bị khóa hoặc chưa kích hoạt (status != ACTIVE)
        var userId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        _currentUserService.UserId.Returns(userId);

        var club = new ClubGroup(groupName: "Hội Cầu Lông Hà Nội", description: "CLB Cầu Lông", code: "HCLHN", isActive: true);
        typeof(ClubGroup).GetProperty("Id")?.SetValue(club, clubId);

        var memberRole = new GroupRole(clubId, "Thành viên");
        typeof(GroupRole).GetProperty("Id")?.SetValue(memberRole, roleId);

        var membership = new GroupMember(clubId, userId, roleId, status: ClubMemberStatus.BANNED);
        typeof(GroupMember).GetProperty("Group")?.SetValue(membership, club);
        typeof(GroupMember).GetProperty("RoleEntity")?.SetValue(membership, memberRole);

        _clubMemberRepository.FindSingleAsync(
            Arg.Any<Expression<Func<GroupMember, bool>>>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<Expression<Func<GroupMember, object>>[]>())
            .Returns(membership);

        var command = new SwitchClubContextCommand(clubId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ClubMember.Inactive");
    }

    [Fact]
    [Trait("Category", "UnitTest")]
    public async Task Handle_WhenUserIsActiveMember_ShouldReturnSwitchClubResponseWithTokensAndPermissions()
    {
        // Arrange: Thành viên hợp lệ, trạng thái ACTIVE -> Nhận Token và Permissions
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
}
