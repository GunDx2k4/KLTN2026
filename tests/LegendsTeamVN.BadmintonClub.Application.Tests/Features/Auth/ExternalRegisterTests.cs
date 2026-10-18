using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Auth;
using LegendsTeamVN.BadmintonClub.Application.Features.Auth.ExternalRegister;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Caching;
using LegendsTeamVN.Core.Identity.Abstractions;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Auth;

public class ExternalRegisterTests
{
    private readonly IExternalAuthService _externalAuthService = Substitute.For<IExternalAuthService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUserManagerService _userManagerService = Substitute.For<IUserManagerService>();
    private readonly IJwtTokenService _jwtTokenService = Substitute.For<IJwtTokenService>();
    private readonly ICacheService _cacheService = Substitute.For<ICacheService>();
    private readonly ExternalRegisterCommandHandler _handler;

    public ExternalRegisterTests()
    {
        _handler = new ExternalRegisterCommandHandler(
            _externalAuthService,
            _userRepository,
            _userManagerService,
            _jwtTokenService,
            _cacheService);
    }

    [Fact]
    public async Task Handle_WhenValidExternalToken_ShouldCreateUserAndIssueJwtToken()
    {
        // Arrange
        var provider = "Google";
        var idToken = "google-valid-token";
        var externalInfo = new ExternalAuthUserInfo(
            Provider: provider,
            ProviderKey: "google-uid-12345",
            Email: "player.google@gmail.com",
            FullName: "Nguyễn Văn Google",
            AvatarUrl: "https://lh3.googleusercontent.com/avatar.jpg"
        );

        _externalAuthService.VerifyTokenAsync(idToken, provider, Arg.Any<CancellationToken>())
            .Returns(externalInfo);

        var newUserId = Guid.NewGuid();
        _userManagerService.FindByExternalLoginAsync(provider, externalInfo.ProviderKey)
            .Returns((LegendsTeamVN.Core.Identity.Entities.AppUser?)null);

        _userManagerService.CreateExternalUserAsync(provider, externalInfo.ProviderKey, externalInfo.Email, externalInfo.FullName)
            .Returns((true, newUserId, Array.Empty<string>()));

        _userRepository.FindByIdAsync(newUserId, Arg.Any<CancellationToken>())
            .Returns((LegendsTeamVN.BadmintonClub.Domain.Entities.AppUser?)null);
        _userRepository.FindByEmailOrPhoneAsync(externalInfo.Email, Arg.Any<CancellationToken>())
            .Returns((LegendsTeamVN.BadmintonClub.Domain.Entities.AppUser?)null);

        _userManagerService.GetRolesAsync(newUserId).Returns(["Member"]);
        _userManagerService.GetPermissionsAsync(newUserId).Returns([]);

        _jwtTokenService.GenerateAccessToken(newUserId, externalInfo.Email, externalInfo.FullName, Arg.Any<IList<string>>(), null, Arg.Any<IList<string>>())
            .Returns("access-token-jwt");
        _jwtTokenService.GenerateRefreshToken().Returns("refresh-token-xyz");

        var command = new ExternalRegisterCommand(idToken, provider);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(newUserId);
        result.Value.Email.Should().Be("player.google@gmail.com");
        result.Value.FullName.Should().Be("Nguyễn Văn Google");
        result.Value.AccessToken.Should().Be("access-token-jwt");

        _userRepository.Received(1).Add(Arg.Is<LegendsTeamVN.BadmintonClub.Domain.Entities.AppUser>(u => u.Id == newUserId && u.EmailAddress == externalInfo.Email));
    }

    [Fact]
    public async Task Handle_WhenExternalTokenInvalid_ShouldReturnUnauthorizedError()
    {
        // Arrange
        var provider = "Google";
        var invalidToken = "invalid-token";

        _externalAuthService.VerifyTokenAsync(invalidToken, provider, Arg.Any<CancellationToken>())
            .Returns((ExternalAuthUserInfo?)null);

        var command = new ExternalRegisterCommand(invalidToken, provider);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidExternalToken");
    }
}
