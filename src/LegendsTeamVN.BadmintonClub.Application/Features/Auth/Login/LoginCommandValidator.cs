using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Auth.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Identifier)
            .NotEmpty().WithMessage("Số điện thoại hoặc Email không được để trống.")
            .MaximumLength(255).WithMessage("Tài khoản không được vượt quá 255 ký tự.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.");
    }
}
