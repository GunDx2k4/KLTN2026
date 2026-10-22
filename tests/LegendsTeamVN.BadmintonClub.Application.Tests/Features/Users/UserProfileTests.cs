using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Users.Requests;
using LegendsTeamVN.BadmintonClub.Application.Features.Users.Profile.Update;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Users;

public class UserProfileTests
{
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUserManagerService _userManagerService = Substitute.For<IUserManagerService>();
    private readonly UpdateUserProfileCommandHandler _handler;

    public UserProfileTests()
    {
        _handler = new UpdateUserProfileCommandHandler(
            _currentUserService,
            _userRepository,
            _userManagerService);
    }

    [Fact]
    public void AppUser_UpdateProfile_ShouldUpdateFieldsCorrectly()
    {
        // Arrange
        var user = AppUser.CreateStandard(
            Guid.NewGuid(),
            "User Ban Dau",
            "test@example.com",
            "0900000000",
            1,
            SkillLevel.NB
        );

        // Act: fullName="Nguyễn Công Hoan", gender=0, skillLevel=5 (TBK)
        user.UpdateProfile("Nguyễn Công Hoan", 0, "https://avatar.com/1.png", SkillLevel.TBK, true);

        // Assert
        user.FullName.Should().Be("Nguyễn Công Hoan");
        user.Gender.Should().Be((short)0);
        user.SkillLevel.Should().Be(SkillLevel.TBK);
        user.IsOpenForGuest.Should().BeTrue();
        user.AvatarUrl.Should().Be("https://avatar.com/1.png");
    }

    [Fact]
    public async Task Handle_WhenUserExists_ShouldUpdateProfileInRepository()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserService.UserId.Returns(userId);

        var existingUser = AppUser.CreateStandard(
            userId,
            "Cũ",
            "old@example.com",
            "0911111111",
            1,
            SkillLevel.NB
        );

        _userRepository.FindByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(existingUser);

        var request = new UpdateUserProfileRequest(
            FullName: "Nguyễn Công Hoan",
            Gender: 0,
            AvatarUrl: null,
            SkillLevel: SkillLevel.TBK,
            IsOpenForGuest: true
        );

        var command = new UpdateUserProfileCommand(request);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        existingUser.FullName.Should().Be("Nguyễn Công Hoan");
        existingUser.Gender.Should().Be((short)0);
        existingUser.SkillLevel.Should().Be(SkillLevel.TBK);
        _userRepository.Received(1).Update(existingUser);
    }
}
