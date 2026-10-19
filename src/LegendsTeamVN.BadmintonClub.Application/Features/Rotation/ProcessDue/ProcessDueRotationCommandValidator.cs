using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.ProcessDue;

public sealed class ProcessDueRotationCommandValidator : AbstractValidator<ProcessDueRotationCommand>
{
    public ProcessDueRotationCommandValidator() => RuleFor(x => x.SessionId).NotEmpty();
}
