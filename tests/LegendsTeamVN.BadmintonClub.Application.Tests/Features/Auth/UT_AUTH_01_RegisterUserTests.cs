using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Auth;
using LegendsTeamVN.BadmintonClub.Application.Features.Auth.Register;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Caching;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Identity.DTOs;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Auth;

public class UT_AUTH_01_RegisterUserTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUserManagerService _userManagerService = Substitute.For<IUserManagerService>();
    private readonly IJwtTokenService _jwtTokenService = Substitute.For<IJwtTokenService>();
    private readonly ICacheService _cacheService = Substitute.For<ICacheService>();
    private readonly RegisterCommandHandler _handler;

    public UT_AUTH_01_RegisterUserTests()
    {
        _handler = new RegisterCommandHandler(
            _userRepository,
            _userManagerService,
            _jwtTokenService,
            _cacheService);
    }

    [Fact]
    public async Task Handle_WithValidPhoneAndInfo_ShouldCreateUserAndReturnTokens()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new RegisterCommand(
            FullName: "Nguyễn Xuân Dũng",
            Email: null,
            PhoneNumber: "0912345678",
            Password: "Abc@12345",
            Gender: 1,
            SkillLevel: SkillLevel.TB
        );

        _userRepository.IsUserExistAsync(command.PhoneNumber, command.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        _userManagerService.CreateUserAsync(
            command.PhoneNumber!,
            command.Email,
            command.PhoneNumber,
            command.Password)
            .Returns((true, userId, Array.Empty<string>()));

        _userManagerService.GetRolesAsync(userId)
            .Returns(new List<string> { "Member" });

        _userManagerService.GetPermissionsAsync(userId)
            .Returns(new List<string>());

        _jwtTokenService.GenerateAccessToken(
            userId,
            command.PhoneNumber!,
            command.PhoneNumber!,
            Arg.Any<List<string>>(),
            null,
            Arg.Any<List<string>>())
            .Returns("mock_access_token");

        _jwtTokenService.GenerateRefreshToken()
            .Returns("mock_refresh_token");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.UserId.Should().Be(userId);
        result.Value.FullName.Should().Be("Nguyễn Xuân Dũng");
        result.Value.PhoneNumber.Should().Be("0912345678");
        result.Value.SkillLevel.Should().Be(SkillLevel.TB.ToString());
        result.Value.ReputationScore.Should().Be(100.00m);
        result.Value.AccessToken.Should().Be("mock_access_token");
        result.Value.RefreshToken.Should().Be("mock_refresh_token");

        _userRepository.Received(1).Add(Arg.Is<AppUser>(u =>
            u.Id == userId &&
            u.FullName == "Nguyễn Xuân Dũng" &&
            u.PhoneNumber == "0912345678" &&
            u.ReputationScore == 100.00m &&
            u.IsActive &&
            u.SkillLevel == SkillLevel.TB));

        await _cacheService.Received(1).SetAsync(
            command.PhoneNumber!,
            Arg.Is<AuthenticationResponse>(a => a.AccessToken == "mock_access_token" && a.RefreshToken == "mock_refresh_token"),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }
}
