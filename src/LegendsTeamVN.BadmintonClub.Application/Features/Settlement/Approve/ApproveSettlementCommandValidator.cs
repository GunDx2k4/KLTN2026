using LegendsTeamVN.BadmintonClub.Application.Features.Settlement.Validation;
using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Settlement.Approve;

public sealed class ApproveSettlementCommandValidator : AbstractValidator<ApproveSettlementCommand>
{
    public ApproveSettlementCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Input).NotNull().SetValidator(new SettlementInputValidator());
        RuleFor(x => x.PreviewToken).NotEmpty().MaximumLength(16000);
    }
}
