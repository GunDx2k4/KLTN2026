using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Finish;

public sealed class FinishRotationMatchCommandValidator : AbstractValidator<FinishRotationMatchCommand>
{
    public FinishRotationMatchCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.MatchId).NotEmpty();
    }
}
