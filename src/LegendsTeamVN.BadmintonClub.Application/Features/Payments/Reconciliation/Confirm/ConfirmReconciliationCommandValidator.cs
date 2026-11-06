using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.Reconciliation.Confirm;

public sealed class ConfirmReconciliationCommandValidator : AbstractValidator<ConfirmReconciliationCommand>
{
    public ConfirmReconciliationCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty().WithMessage("Nhóm không hợp lệ.");
        RuleFor(x => x.TransactionId).NotEmpty().WithMessage("Giao dịch không hợp lệ.");
        RuleFor(x => x.FeeId).NotEmpty().WithMessage("Khoản thu không hợp lệ.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Vui lòng nhập lý do đối soát.")
            .MaximumLength(2000).WithMessage("Lý do đối soát không được vượt 2.000 ký tự.");
    }
}
