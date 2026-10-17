using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Create;

public sealed class CreateClubCommandValidator : AbstractValidator<CreateClubCommand>
{
    public CreateClubCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên câu lạc bộ không được để trống.")
            .MaximumLength(255).WithMessage("Tên câu lạc bộ không quá 255 ký tự.");

        RuleFor(x => x.MinSkillLevel)
            .Must((cmd, min) => (int)min <= (int)cmd.MaxSkillLevel)
            .WithMessage("Trình độ tối thiểu không được lớn hơn trình độ tối đa.");

        When(x => x.Schedules != null && x.Schedules.Count > 0, () =>
        {
            RuleForEach(x => x.Schedules).ChildRules(schedule =>
            {
                schedule.RuleFor(s => s.DayOfWeek)
                    .InclusiveBetween((short)0, (short)6)
                    .WithMessage("Thứ trong tuần không hợp lệ (0 = Chủ Nhật, 1..6 = Thứ 2 đến Thứ 7).");

                schedule.RuleFor(s => s.StartTime)
                    .LessThan(s => s.EndTime)
                    .WithMessage("Giờ bắt đầu phải nhỏ hơn giờ kết thúc.");
            });
        });
    }
}
