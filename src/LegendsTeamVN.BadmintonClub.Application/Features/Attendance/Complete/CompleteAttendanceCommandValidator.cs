using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Attendance.Complete;

public sealed class CompleteAttendanceCommandValidator : AbstractValidator<CompleteAttendanceCommand>
{
    public CompleteAttendanceCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
    }
}
