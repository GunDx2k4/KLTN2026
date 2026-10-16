using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.UpdateMemberRole;

public sealed class UpdateMemberRoleCommandValidator : AbstractValidator<UpdateMemberRoleCommand>
{
    public UpdateMemberRoleCommandValidator()
    {
        RuleFor(x => x.ClubId).NotEmpty().WithMessage("ClubId không được để trống.");
        RuleFor(x => x.TargetUserId).NotEmpty().WithMessage("TargetUserId không được để trống.");
        RuleFor(x => x).Must(x => (x.RoleId.HasValue && x.RoleId.Value != Guid.Empty) || x.Role.HasValue || !string.IsNullOrWhiteSpace(x.RoleName))
            .WithMessage("RoleId hoặc vai trò không được để trống.");
    }
}
