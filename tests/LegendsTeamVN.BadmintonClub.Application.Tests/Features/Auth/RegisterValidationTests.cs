using FluentAssertions;
using FluentValidation.TestHelper;
using LegendsTeamVN.BadmintonClub.Application.Features.Auth.Register;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Caching;
using LegendsTeamVN.Core.Identity.Abstractions;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Auth;

public class RegisterValidationTests
{
    private readonly RegisterCommandValidator _validator = new();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUserManagerService _userManagerService = Substitute.For<IUserManagerService>();
    private readonly IJwtTokenService _jwtTokenService = Substitute.For<IJwtTokenService>();
    private readonly ICacheService _cacheService = Substitute.For<ICacheService>();
    private readonly RegisterCommandHandler _handler;

    public RegisterValidationTests()
    {
        _handler = new RegisterCommandHandler(
            _userRepository,
            _userManagerService,
            _jwtTokenService,
            _cacheService);
    }

    [Fact]
    public void Validate_WhenSkillLevelIsOutOfRange_ShouldHaveValidationError()
    {
        // Arrange: skillLevel = 8 (ngoài miền 1..7)
        var command = new RegisterCommand(
            FullName: "Nguyễn Xuân Dũng",
            Email: "dung@example.com",
            PhoneNumber: "0912345678",
            Password: "Abc@12345",
            Gender: 1,
            SkillLevel: (SkillLevel)8
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SkillLevel)
            .WithErrorMessage("Trình độ cầu lông không hợp lệ.");
    }

    [Fact]
    public void Validate_WhenBothPhoneAndEmailAreMissing_ShouldHaveValidationError()
    {
        // Arrange
        var command = new RegisterCommand(
            FullName: "Nguyễn Xuân Dũng",
            Email: null,
            PhoneNumber: null,
            Password: "Abc@12345",
            Gender: 1,
            SkillLevel: SkillLevel.TB
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x)
            .WithErrorMessage("Phải cung cấp ít nhất Email hoặc Số điện thoại để đăng ký.");
    }

    [Fact]
    public void Validate_WhenPasswordIsTooShort_ShouldHaveValidationError()
    {
        // Arrange
        var command = new RegisterCommand(
            FullName: "Nguyễn Xuân Dũng",
            Email: "dung@example.com",
            PhoneNumber: "0912345678",
            Password: "123",
            Gender: 1,
            SkillLevel: SkillLevel.TB
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Mật khẩu phải có ít nhất 6 ký tự.");
    }

    [Fact]
    public async Task Handle_WhenPhoneOrEmailAlreadyExists_ShouldReturnConflictError()
    {
        // Arrange
        var command = new RegisterCommand(
            FullName: "Nguyễn Xuân Dũng",
            Email: null,
            PhoneNumber: "0912345678",
            Password: "Abc@12345",
            Gender: 1,
            SkillLevel: SkillLevel.TB
        );

        _userRepository.IsUserExistAsync(command.PhoneNumber, command.Email, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.AlreadyExists");
        result.Error.Message.Should().Contain("Tài khoản với số điện thoại hoặc email này đã tồn tại");
        _userRepository.DidNotReceive().Add(Arg.Any<LegendsTeamVN.BadmintonClub.Domain.Entities.AppUser>());
    }
}
