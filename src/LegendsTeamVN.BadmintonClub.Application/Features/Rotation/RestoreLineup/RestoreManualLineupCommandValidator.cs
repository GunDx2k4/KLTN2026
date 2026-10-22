using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.RestoreLineup;

public sealed class RestoreManualLineupCommandValidator : AbstractValidator<RestoreManualLineupCommand>
{
    public RestoreManualLineupCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.MatchId).NotEmpty();
        RuleFor(x => x.ExpectedRotationVersion).NotNull().GreaterThanOrEqualTo(0);
    }
}
