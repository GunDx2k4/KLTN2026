using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger.IssueMonthlyContribution;

public sealed class IssueMonthlyContributionCommandValidator : AbstractValidator<IssueMonthlyContributionCommand>
{
    public IssueMonthlyContributionCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.ContributionMonth).Must(m => m.Day == 1 && m.Year < 9999);
    }
}
