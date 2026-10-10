using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Auth.ExternalRegister;

public sealed class ExternalRegisterCommandValidator : AbstractValidator<ExternalRegisterCommand>
{
    public ExternalRegisterCommandValidator()
    {
        RuleFor(x => x.IdToken)
            .NotEmpty().WithMessage("ID Token bên thứ ba không được để trống.");

        RuleFor(x => x.Provider)
            .NotEmpty().WithMessage("Nhà cung cấp xác thực không được để trống.");
    }
}
