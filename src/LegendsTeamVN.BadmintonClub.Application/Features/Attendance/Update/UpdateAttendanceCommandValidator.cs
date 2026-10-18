using FluentValidation;
using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Attendance.Update;

public sealed class UpdateAttendanceCommandValidator : AbstractValidator<UpdateAttendanceCommand>
{
    public UpdateAttendanceCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Status).Must(status => status is BookingStatus.CHECKED_IN or BookingStatus.NO_SHOW)
            .WithMessage("Chỉ chấp nhận CHECKED_IN hoặc NO_SHOW.");
    }
}
