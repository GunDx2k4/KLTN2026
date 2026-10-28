using FluentValidation.TestHelper;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;
using LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Create;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Sessions;

public class CreateSessionConstraintTests
{
    private readonly CreateSessionCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenEndTimeIsBeforeStartTime_ShouldHaveValidationError()
    {
        // Arrange: start_time = 19:00, end_time = 18:00 (sai logic thời gian)
        var today = DateTime.UtcNow.Date;
        var startTime = today.AddHours(19);
        var endTime = today.AddHours(18);

        var request = new CreateSessionRequest(
            Title: "Buổi sinh hoạt lỗi giờ",
            StartTime: startTime,
            EndTime: endTime,
            CourtLocation: "Sân Cầu Lông Ba Đình",
            CourtCount: 2,
            TargetPlayerPerCourt: 6
        );

        var command = new CreateSessionCommand(Guid.NewGuid(), request);

        // Act
        var result = _validator.TestValidate(command);

        // Assert: Chặn không cho phép tạo buổi chơi sai logic thời gian
        result.ShouldHaveValidationErrorFor(x => x.Request.EndTime)
            .WithErrorMessage("Thời gian kết thúc phải sau thời gian bắt đầu.");
    }

    [Fact]
    public void Validate_WhenEndTimeEqualsStartTime_ShouldHaveValidationError()
    {
        // Arrange: start_time = 19:00, end_time = 19:00
        var today = DateTime.UtcNow.Date;
        var startTime = today.AddHours(19);
        var endTime = today.AddHours(19);

        var request = new CreateSessionRequest(
            Title: "Buổi sinh hoạt thời lượng 0",
            StartTime: startTime,
            EndTime: endTime,
            CourtLocation: "Sân Cầu Lông Ba Đình",
            CourtCount: 2,
            TargetPlayerPerCourt: 6
        );

        var command = new CreateSessionCommand(Guid.NewGuid(), request);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Request.EndTime)
            .WithErrorMessage("Thời gian kết thúc phải sau thời gian bắt đầu.");
    }

    [Fact]
    public void Validate_WhenCourtCountIsZeroOrNegative_ShouldHaveValidationError()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var request = new CreateSessionRequest(
            Title: "Buổi sinh hoạt thiếu sân",
            StartTime: today.AddHours(18),
            EndTime: today.AddHours(20),
            CourtLocation: "Sân Cầu Lông",
            CourtCount: 0,
            TargetPlayerPerCourt: 6
        );

        var command = new CreateSessionCommand(Guid.NewGuid(), request);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Request.CourtCount)
            .WithErrorMessage("Số lượng sân phải lớn hơn 0.");
    }

    [Fact]
    public void Validate_WhenValidTimes_ShouldNotHaveTimeValidationError()
    {
        // Arrange: start_time = 18:00, end_time = 20:00 (hợp lệ)
        var today = DateTime.UtcNow.Date;
        var request = new CreateSessionRequest(
            Title: "Buổi sinh hoạt hợp lệ",
            StartTime: today.AddHours(18),
            EndTime: today.AddHours(20),
            CourtLocation: "Sân Cầu Lông Chuẩn",
            CourtCount: 2,
            TargetPlayerPerCourt: 6
        );

        var command = new CreateSessionCommand(Guid.NewGuid(), request);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Request.EndTime);
        result.ShouldNotHaveValidationErrorFor(x => x.Request.StartTime);
    }
}
