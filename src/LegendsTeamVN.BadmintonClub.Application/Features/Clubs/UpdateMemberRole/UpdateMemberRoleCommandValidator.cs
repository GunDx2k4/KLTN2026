using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.UpdateMemberRole;

public sealed class UpdateMemberRoleCommandValidator : AbstractValidator<UpdateMemberRoleCommand>
{
    public UpdateMemberRoleCommandValidator()
    {
        RuleFor(x => x.ClubId).NotEmpty().WithMessage("ClubId không được để trống.");
        RuleFor(x => x.TargetUserId).NotEmpty().WithMessage("TargetUserId không được để trống.");
        RuleFor(x => x.RoleId).NotEmpty().WithMessage("RoleId không được để trống.");
    }
}
