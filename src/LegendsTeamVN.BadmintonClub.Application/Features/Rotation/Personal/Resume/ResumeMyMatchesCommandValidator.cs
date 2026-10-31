using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Personal.Resume;

public sealed class ResumeMyMatchesCommandValidator : AbstractValidator<ResumeMyMatchesCommand>
{
    public ResumeMyMatchesCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.ExpectedRotationVersion).NotNull().GreaterThanOrEqualTo(0);
    }
}
