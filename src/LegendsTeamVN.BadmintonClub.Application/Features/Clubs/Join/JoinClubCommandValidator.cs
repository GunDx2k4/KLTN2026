using FluentValidation;
using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Join;

public sealed class JoinClubCommandValidator : AbstractValidator<JoinClubCommand>
{
    public JoinClubCommandValidator()
    {
        RuleFor(x => x.ClubCode)
            .NotEmpty().WithMessage("Mã câu lạc bộ không được để trống.")
            .MaximumLength(50).WithMessage("Mã câu lạc bộ không được vượt quá 50 ký tự.");

        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("Vai trò không hợp lệ.")
            .Must(r => r != ClubRole.Host).WithMessage("Không thể tự tham gia với vai trò Host (Chủ phòng).");

        RuleFor(x => x.Nickname)
            .MaximumLength(100).WithMessage("Biệt danh không được vượt quá 100 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.Nickname));
    }
}
