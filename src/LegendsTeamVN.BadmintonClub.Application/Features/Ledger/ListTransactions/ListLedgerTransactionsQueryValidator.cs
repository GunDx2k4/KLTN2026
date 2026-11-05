using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Requests;
using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger.ListTransactions;

public sealed class ListLedgerTransactionsQueryValidator : AbstractValidator<ListLedgerTransactionsQuery>
{
    public ListLedgerTransactionsQueryValidator()
    {
        RuleFor(x => x.ClubId).NotEmpty().WithMessage("Nhóm không hợp lệ.");
        RuleFor(x => x.Filter).NotNull().SetValidator(new LedgerReadFilterValidator());
    }
}
