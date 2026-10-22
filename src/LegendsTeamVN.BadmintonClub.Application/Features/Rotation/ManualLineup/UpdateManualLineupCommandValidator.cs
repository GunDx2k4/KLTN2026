using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.ManualLineup;

public sealed class UpdateManualLineupCommandValidator : AbstractValidator<UpdateManualLineupCommand>
{
    public UpdateManualLineupCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.MatchId).NotEmpty();
        RuleFor(x => x.ExpectedRotationVersion).NotNull().GreaterThanOrEqualTo(0);
        RuleFor(x => x.TeamA).Must(t => t is { Count: 2 } && t.All(id => id != Guid.Empty));
        RuleFor(x => x.TeamB).Must(t => t is { Count: 2 } && t.All(id => id != Guid.Empty));
        RuleFor(x => x).Must(x => x.TeamA is not null && x.TeamB is not null &&
            x.TeamA.Concat(x.TeamB).Distinct().Count() == 4).WithMessage("Đội hình phải có 4 UserId khác nhau.");
    }
}
