using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Personal.Pause;

public sealed class PauseMyMatchesCommandValidator : AbstractValidator<PauseMyMatchesCommand>
{
    public PauseMyMatchesCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.ExpectedRotationVersion).NotNull().GreaterThanOrEqualTo(0);
    }
}
