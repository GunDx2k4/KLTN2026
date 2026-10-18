using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Initialize;

public sealed class InitializeRotationCommandValidator : AbstractValidator<InitializeRotationCommand>
{
    public InitializeRotationCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
    }
}
