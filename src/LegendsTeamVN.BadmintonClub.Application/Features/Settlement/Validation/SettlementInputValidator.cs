using FluentValidation;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Settlement.Requests;
using LegendsTeamVN.BadmintonClub.Domain.Services;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Settlement.Validation;

public sealed class SettlementInputValidator : AbstractValidator<PreviewSettlementRequest>
{
    public SettlementInputValidator()
    {
        RuleFor(x => x.ShuttlecockUsed).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ShuttlecockPrice).Must(SettlementAllocationEngine.IsValidAmount);
        RuleFor(x => x.CourtRentalCost).Must(SettlementAllocationEngine.IsValidAmount);
        RuleFor(x => x.ExtraExpense).Must(SettlementAllocationEngine.IsValidAmount);
        RuleFor(x => x.Overrides).NotNull();
        RuleForEach(x => x.Overrides).NotNull();
        RuleForEach(x => x.Overrides).ChildRules(line =>
        {
            line.RuleFor(x => x.UserId).NotEmpty();
            line.RuleFor(x => x.FinalAmount).Must(SettlementAllocationEngine.IsValidAmount);
            line.RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
        });
    }
}
