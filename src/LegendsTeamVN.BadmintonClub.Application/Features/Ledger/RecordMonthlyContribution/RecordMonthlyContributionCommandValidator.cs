using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger.RecordMonthlyContribution;

public sealed class RecordMonthlyContributionCommandValidator : AbstractValidator<RecordMonthlyContributionCommand>
{
    public RecordMonthlyContributionCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.MonthlyContributionId).NotEmpty();
        RuleFor(x => x.RequestId).NotEmpty();
    }
}
