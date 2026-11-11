using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Requests;
using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger.ListMonthlyContributionCandidates;

public sealed class ListMonthlyContributionCandidatesQueryValidator : AbstractValidator<ListMonthlyContributionCandidatesQuery>
{
    public ListMonthlyContributionCandidatesQueryValidator()
    {
        RuleFor(x => x.ClubId).NotEmpty().WithMessage("Nhóm không hợp lệ.");
        RuleFor(x => x.Filter).NotNull().SetValidator(new LedgerReadFilterValidator());
    }
}
