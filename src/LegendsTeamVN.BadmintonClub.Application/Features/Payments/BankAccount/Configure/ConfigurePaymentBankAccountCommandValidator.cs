using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.BankAccount.Configure;

public sealed class ConfigurePaymentBankAccountCommandValidator : AbstractValidator<ConfigurePaymentBankAccountCommand>
{
    public ConfigurePaymentBankAccountCommandValidator()
    {
        RuleFor(x => x.ClubId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
        When(x => x.Input is not null, () =>
        {
            RuleFor(x => x.Input.BankBin).NotEmpty().Matches("^[0-9]{6}$");
            RuleFor(x => x.Input.AccountNumber).NotEmpty().Matches("^[0-9]{1,19}$");
            RuleFor(x => x.Input.BankName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Input.AccountHolder).NotEmpty().MaximumLength(255);
        });
    }
}
