using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Generate;

public sealed class GenerateRotationProposalsCommandValidator : AbstractValidator<GenerateRotationProposalsCommand>
{
    public GenerateRotationProposalsCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
    }
}
