using FluentValidation;
using LegendsTeamVN.BadmintonClub.Domain.Entities;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger.CreateExpense;

public sealed class CreateLedgerExpenseCommandValidator : AbstractValidator<CreateLedgerExpenseCommand>
{
    public CreateLedgerExpenseCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.SessionId).NotEqual(Guid.Empty).When(x => x.SessionId.HasValue);
        RuleFor(x => x.Content).Must(x => x is null || x.Length <= LedgerAttachment.MaximumContentLength);
        RuleFor(x => x).Must(x => x.Content is null ? x.FileName is null && x.ContentType is null :
            !string.IsNullOrWhiteSpace(x.FileName) && !string.IsNullOrWhiteSpace(x.ContentType));
    }
}
