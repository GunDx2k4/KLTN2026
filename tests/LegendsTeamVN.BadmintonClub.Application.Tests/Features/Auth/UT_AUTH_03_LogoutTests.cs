using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Application.Features.Auth.Logout;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Caching;
using LegendsTeamVN.Core.Identity.Abstractions;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Auth;

public class UT_AUTH_03_LogoutTests
{
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IDeviceRepository _deviceRepository = Substitute.For<IDeviceRepository>();
    private readonly ICacheService _cacheService = Substitute.For<ICacheService>();
    private readonly LogoutCommandHandler _handler;

    public UT_AUTH_03_LogoutTests()
    {
        _handler = new LogoutCommandHandler(
            _currentUserService,
            _deviceRepository,
            _cacheService);
    }

    [Fact]
    public async Task Handle_WithValidUserAndDeviceToken_ShouldDeactivateDeviceAndBlacklistToken()
    {
        // Arrange
        var userId = Guid.NewGuid();
        const string deviceToken = "UUID_DEV_1";
        const string email = "user1@example.com";
        const string accessToken = "access_token_xyz";

        _currentUserService.UserId.Returns(userId);
        _currentUserService.Email.Returns(email);

        var command = new LogoutCommand(deviceToken, accessToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        await _deviceRepository.Received(1).DeactivateDeviceAsync(userId, deviceToken, Arg.Any<CancellationToken>());
        await _cacheService.Received(1).RemoveAsync(email, Arg.Any<CancellationToken>());
        await _cacheService.Received(1).SetAsync($"blacklist_token:{accessToken}", true, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoDeviceTokenProvided_ShouldOnlyClearCacheAndBlacklistToken()
    {
        // Arrange
        var userId = Guid.NewGuid();
        const string email = "user2@example.com";
        const string accessToken = "access_token_abc";

        _currentUserService.UserId.Returns(userId);
        _currentUserService.Email.Returns(email);

        var command = new LogoutCommand(null, accessToken);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _deviceRepository.DidNotReceive().DeactivateDeviceAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _cacheService.Received(1).RemoveAsync(email, Arg.Any<CancellationToken>());
        await _cacheService.Received(1).SetAsync($"blacklist_token:{accessToken}", true, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }
}
