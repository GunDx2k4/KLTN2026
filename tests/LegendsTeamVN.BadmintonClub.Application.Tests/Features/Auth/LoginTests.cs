using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Auth;
using LegendsTeamVN.BadmintonClub.Application.Features.Auth.Login;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Caching;
using LegendsTeamVN.Core.Identity.Abstractions;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Auth;

public class LoginTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IDeviceRepository _deviceRepository = Substitute.For<IDeviceRepository>();
    private readonly IUserManagerService _userManagerService = Substitute.For<IUserManagerService>();
    private readonly IJwtTokenService _jwtTokenService = Substitute.For<IJwtTokenService>();
    private readonly ICacheService _cacheService = Substitute.For<ICacheService>();
    private readonly LoginCommandHandler _handler;

    public LoginTests()
    {
        _handler = new LoginCommandHandler(
            _userRepository,
            _deviceRepository,
            _userManagerService,
            _jwtTokenService,
            _cacheService);
    }

    [Fact]
    public async Task Handle_WithValidCredentials_ShouldReturnLoginResponseWithTokens()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new LoginCommand("0912345678", "Abc@12345", DeviceToken: "token_123", Platform: "android");
        var appUser = new AppUser(
            id: userId,
            fullName: "Nguyễn Xuân Dũng",
            phoneNumber: "0912345678",
            isActive: true,
            skillLevel: SkillLevel.TB
        );

        _userManagerService.CheckPasswordAsync(command.Identifier, command.Password)
            .Returns((true, userId));

        _userRepository.FindByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(appUser);

        _userManagerService.GetRolesAsync(userId)
            .Returns(new List<string> { "Member" });

        _userManagerService.GetPermissionsAsync(userId)
            .Returns(new List<string>());

        _jwtTokenService.GenerateAccessToken(userId, "0912345678", "Nguyễn Xuân Dũng", Arg.Any<List<string>>(), null, Arg.Any<List<string>>())
            .Returns("login_access_token");

        _jwtTokenService.GenerateRefreshToken()
            .Returns("login_refresh_token");

        _userRepository.GetUserWithMembershipsAsync(userId, Arg.Any<CancellationToken>())
            .Returns(appUser);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.UserId.Should().Be(userId);
        result.Value.AccessToken.Should().Be("login_access_token");
        result.Value.RefreshToken.Should().Be("login_refresh_token");

        await _deviceRepository.Received(1).UpsertDeviceAsync(
            userId,
            "token_123",
            DevicePlatform.ANDROID,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithWrongPassword_ShouldReturnUnauthorized()
    {
        // Arrange
        var command = new LoginCommand("0912345678", "WrongPass");

        _userManagerService.CheckPasswordAsync(command.Identifier, command.Password)
            .Returns((false, null));
        _userRepository.FindByEmailOrPhoneAsync(command.Identifier, Arg.Any<CancellationToken>())
            .Returns((AppUser?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.Unauthorized");
    }
}
