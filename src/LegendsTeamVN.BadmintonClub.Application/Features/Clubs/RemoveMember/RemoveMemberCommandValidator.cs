using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.RemoveMember;

public sealed class RemoveMemberCommandValidator : AbstractValidator<RemoveMemberCommand>
{
    public RemoveMemberCommandValidator()
    {
        RuleFor(x => x.ClubId).NotEmpty().WithMessage("ClubId không được để trống.");
        RuleFor(x => x.TargetUserId).NotEmpty().WithMessage("TargetUserId không được để trống.");
    }
}
