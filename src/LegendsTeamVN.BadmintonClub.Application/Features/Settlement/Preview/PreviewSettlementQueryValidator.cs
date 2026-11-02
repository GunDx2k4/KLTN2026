using LegendsTeamVN.BadmintonClub.Application.Features.Settlement.Validation;
using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Settlement.Preview;

public sealed class PreviewSettlementQueryValidator : AbstractValidator<PreviewSettlementQuery>
{
    public PreviewSettlementQueryValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Input).NotNull().SetValidator(new SettlementInputValidator());
    }
}
