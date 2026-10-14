using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Users.Profile.Update;

public sealed class UpdateUserProfileCommandValidator : AbstractValidator<UpdateUserProfileCommand>
{
    public UpdateUserProfileCommandValidator()
    {
        RuleFor(x => x.Request.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
            .MaximumLength(100).WithMessage("Họ và tên không được vượt quá 100 ký tự.");

        RuleFor(x => x.Request.Gender)
            .InclusiveBetween((short)0, (short)2).WithMessage("Giới tính không hợp lệ (0: Khác, 1: Nam, 2: Nữ).");

        RuleFor(x => x.Request.SkillLevel)
            .IsInEnum().WithMessage("Trình độ tự đánh giá không hợp lệ.");
    }
}
