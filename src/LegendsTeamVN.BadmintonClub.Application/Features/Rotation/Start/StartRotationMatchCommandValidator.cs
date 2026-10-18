using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Start;

public sealed class StartRotationMatchCommandValidator : AbstractValidator<StartRotationMatchCommand>
{
    public StartRotationMatchCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.MatchId).NotEmpty();
    }
}
