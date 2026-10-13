using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.JoinRequests.Create;

public sealed class CreateJoinRequestCommandValidator : AbstractValidator<CreateJoinRequestCommand>
{
    public CreateJoinRequestCommandValidator()
    {
        RuleFor(x => x.ClubId)
            .NotEmpty().WithMessage("Mã câu lạc bộ không được để trống.");

        RuleFor(x => x.Introduction)
            .MaximumLength(500).WithMessage("Lời giới thiệu không quá 500 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.Introduction));
    }
}
