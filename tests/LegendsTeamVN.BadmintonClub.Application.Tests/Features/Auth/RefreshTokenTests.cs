using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Application.Features.Auth.Refresh;
using LegendsTeamVN.Core.Application.Caching;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Identity.DTOs;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Auth;

public class RefreshTokenTests
{
    private readonly IUserManagerService _userManagerService = Substitute.For<IUserManagerService>();
    private readonly IJwtTokenService _jwtTokenService = Substitute.For<IJwtTokenService>();
    private readonly ICacheService _cacheService = Substitute.For<ICacheService>();
    private readonly RefreshTokenCommandHandler _handler;

    public RefreshTokenTests()
    {
        _handler = new RefreshTokenCommandHandler(
            _userManagerService,
            _jwtTokenService,
            _cacheService);
    }

    [Fact]
    public async Task Handle_WhenValidRefreshTokenAndSession_ShouldReturnNewTokenPairAndCache()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "player@example.com";
        var userName = "Player One";
        var oldAccessToken = "old-valid-access-token";
        var oldRefreshToken = "old-refresh-token";

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Name, userName)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        _jwtTokenService.GetPrincipalFromExpiredToken(oldAccessToken).Returns(principal);

        var cachedSession = new AuthenticationResponse
        {
            AccessToken = oldAccessToken,
            RefreshToken = oldRefreshToken,
            RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(1)
        };
        _cacheService.GetAsync<AuthenticationResponse>(email, Arg.Any<CancellationToken>())
            .Returns(cachedSession);

        _userManagerService.GetRolesAsync(userId).Returns(["Member"]);
        _userManagerService.GetPermissionsAsync(userId).Returns(["Session.View"]);

        var newAccessToken = "new-fresh-access-token";
        var newRefreshToken = "new-fresh-refresh-token";
        _jwtTokenService.GenerateAccessToken(userId, email, userName, Arg.Any<IList<string>>(), null, Arg.Any<IList<string>>())
            .Returns(newAccessToken);
        _jwtTokenService.GenerateRefreshToken().Returns(newRefreshToken);

        var command = new RefreshTokenCommand(oldAccessToken, oldRefreshToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be(newAccessToken);
        result.Value.RefreshToken.Should().Be(newRefreshToken);

        await _cacheService.Received(1).SetAsync(
            email,
            Arg.Is<AuthenticationResponse>(r => r.AccessToken == newAccessToken && r.RefreshToken == newRefreshToken),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Handle_WhenRefreshTokenDoesNotMatchCache_ShouldReturnTokenInvalid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "player@example.com";
        var oldAccessToken = "old-token";
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email)
        };
        _jwtTokenService.GetPrincipalFromExpiredToken(oldAccessToken).Returns(new ClaimsPrincipal(new ClaimsIdentity(claims)));

        var cachedSession = new AuthenticationResponse
        {
            AccessToken = oldAccessToken,
            RefreshToken = "different-refresh-token",
            RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(1)
        };
        _cacheService.GetAsync<AuthenticationResponse>(email, Arg.Any<CancellationToken>())
            .Returns(cachedSession);

        var command = new RefreshTokenCommand(oldAccessToken, "submitted-invalid-refresh-token");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Token.Invalid");
    }

    [Fact]
    public async Task Handle_WhenRefreshTokenExpiredInCache_ShouldReturnTokenExpired()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "player@example.com";
        var oldAccessToken = "old-token";
        var refreshToken = "my-token";
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email)
        };
        _jwtTokenService.GetPrincipalFromExpiredToken(oldAccessToken).Returns(new ClaimsPrincipal(new ClaimsIdentity(claims)));

        var cachedSession = new AuthenticationResponse
        {
            AccessToken = oldAccessToken,
            RefreshToken = refreshToken,
            RefreshTokenExpiryTime = DateTime.UtcNow.AddMinutes(-5) // Expired!
        };
        _cacheService.GetAsync<AuthenticationResponse>(email, Arg.Any<CancellationToken>())
            .Returns(cachedSession);

        var command = new RefreshTokenCommand(oldAccessToken, refreshToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Token.Expired");
    }
}
