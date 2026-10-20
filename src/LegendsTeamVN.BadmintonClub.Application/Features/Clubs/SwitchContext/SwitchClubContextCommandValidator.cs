using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.SwitchContext;

public sealed class SwitchClubContextCommandValidator : AbstractValidator<SwitchClubContextCommand>
{
    public SwitchClubContextCommandValidator()
    {
        RuleFor(x => x.ClubId)
            .NotEmpty().WithMessage("Mã câu lạc bộ không được để trống.");
    }
}
