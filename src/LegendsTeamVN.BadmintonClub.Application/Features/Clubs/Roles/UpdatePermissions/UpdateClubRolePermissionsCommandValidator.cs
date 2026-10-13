using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Roles.UpdatePermissions;

public sealed class UpdateClubRolePermissionsCommandValidator : AbstractValidator<UpdateClubRolePermissionsCommand>
{
    public UpdateClubRolePermissionsCommandValidator()
    {
        RuleFor(x => x.ClubId)
            .NotEmpty().WithMessage("Mã câu lạc bộ không được để trống.");

        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("Mã vai trò không được để trống.");

        RuleFor(x => x.PermissionIds)
            .NotNull().WithMessage("Danh sách quyền không được là null.");
    }
}
