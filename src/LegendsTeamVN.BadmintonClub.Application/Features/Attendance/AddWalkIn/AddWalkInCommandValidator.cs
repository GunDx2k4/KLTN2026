using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Attendance.AddWalkIn;

public sealed class AddWalkInCommandValidator : AbstractValidator<AddWalkInCommand>
{
    public AddWalkInCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
