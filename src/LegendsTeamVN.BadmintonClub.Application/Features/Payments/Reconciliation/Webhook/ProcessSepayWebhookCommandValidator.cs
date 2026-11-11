using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.Reconciliation.Webhook;

public sealed class ProcessSepayWebhookCommandValidator : AbstractValidator<ProcessSepayWebhookCommand>
{
    public ProcessSepayWebhookCommandValidator()
    {
        RuleFor(x => x.BankAccountId).NotEmpty().WithMessage("Tài khoản nhận không hợp lệ.");
        RuleFor(x => x.Body).NotNull().WithMessage("Thiếu nội dung webhook.")
            .Must(x => x is { Length: > 0 and <= 65536 }).WithMessage("Nội dung webhook phải có từ 1 đến 65.536 byte.");
        RuleFor(x => x.Signature).MaximumLength(100).WithMessage("Chữ ký vượt độ dài cho phép.");
        RuleFor(x => x.Timestamp).MaximumLength(20).WithMessage("Timestamp vượt độ dài cho phép.");
    }
}
