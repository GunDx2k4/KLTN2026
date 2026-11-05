using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Requests;
using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger.ListDebts;

public sealed class ListLedgerDebtsQueryValidator : AbstractValidator<ListLedgerDebtsQuery>
{
    public ListLedgerDebtsQueryValidator()
    {
        RuleFor(x => x.ClubId).NotEmpty().WithMessage("Nhóm không hợp lệ.");
        RuleFor(x => x.Filter).NotNull().SetValidator(new LedgerReadFilterValidator());
    }
}
