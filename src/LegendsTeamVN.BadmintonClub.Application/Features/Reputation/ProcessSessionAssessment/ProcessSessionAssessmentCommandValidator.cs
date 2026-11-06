using FluentValidation;
namespace LegendsTeamVN.BadmintonClub.Application.Features.Reputation.ProcessSessionAssessment;
public sealed class ProcessSessionAssessmentCommandValidator : AbstractValidator<ProcessSessionAssessmentCommand>
{
    public ProcessSessionAssessmentCommandValidator() { RuleFor(x => x.SessionId).NotEmpty(); }
}
