using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Create;

public sealed class CreateClubCommandValidator : AbstractValidator<CreateClubCommand>
{
    public CreateClubCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên câu lạc bộ không được để trống.")
            .MaximumLength(255).WithMessage("Tên câu lạc bộ không quá 255 ký tự.");
    }
}
