using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Roles.Create;

public sealed class CreateClubRoleCommandValidator : AbstractValidator<CreateClubRoleCommand>
{
    public CreateClubRoleCommandValidator()
    {
        RuleFor(x => x.ClubId)
            .NotEmpty().WithMessage("Mã câu lạc bộ không được để trống.");

        RuleFor(x => x.RoleName)
            .NotEmpty().WithMessage("Tên vai trò không được để trống.")
            .MaximumLength(100).WithMessage("Tên vai trò không quá 100 ký tự.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Mô tả không quá 500 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.Description));
    }
}
