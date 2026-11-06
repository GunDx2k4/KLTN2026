using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Requests;
using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger.GetOverview;

public sealed class GetLedgerOverviewQueryValidator : AbstractValidator<GetLedgerOverviewQuery>
{
    public GetLedgerOverviewQueryValidator()
    {
        RuleFor(x => x.ClubId).NotEmpty().WithMessage("Nhóm không hợp lệ.");
        RuleFor(x => x.Filter).NotNull().SetValidator(new LedgerReadFilterValidator());
    }
}
