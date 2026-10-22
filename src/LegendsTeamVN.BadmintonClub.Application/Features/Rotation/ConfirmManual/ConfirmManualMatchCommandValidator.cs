using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.ConfirmManual;

public sealed class ConfirmManualMatchCommandValidator : AbstractValidator<ConfirmManualMatchCommand>
{
    public ConfirmManualMatchCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.MatchId).NotEmpty();
        RuleFor(x => x.ExpectedRotationVersion).NotNull().GreaterThanOrEqualTo(0);
    }
}
