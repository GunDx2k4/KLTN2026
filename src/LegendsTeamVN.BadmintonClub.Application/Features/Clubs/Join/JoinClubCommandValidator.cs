using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Join;

public sealed class JoinClubCommandValidator : AbstractValidator<JoinClubCommand>
{
    public JoinClubCommandValidator()
    {
        RuleFor(x => x.ClubCode)
            .NotEmpty().WithMessage("Mã câu lạc bộ không được để trống.")
            .MaximumLength(50).WithMessage("Mã câu lạc bộ không được vượt quá 50 ký tự.");

        RuleFor(x => x.Nickname)
            .MaximumLength(100).WithMessage("Biệt danh không được vượt quá 100 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.Nickname));
    }
}
