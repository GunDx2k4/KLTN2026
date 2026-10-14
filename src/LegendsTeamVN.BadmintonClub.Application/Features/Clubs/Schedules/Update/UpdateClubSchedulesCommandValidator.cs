using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Schedules.Update;

public sealed class UpdateClubSchedulesCommandValidator : AbstractValidator<UpdateClubSchedulesCommand>
{
    public UpdateClubSchedulesCommandValidator()
    {
        RuleFor(x => x.ClubId)
            .NotEmpty().WithMessage("ID câu lạc bộ không được để trống.");

        RuleForEach(x => x.Request.Schedules).ChildRules(schedule =>
        {
            schedule.RuleFor(s => s.DayOfWeek)
                .InclusiveBetween(0, 6).WithMessage("Thứ trong tuần không hợp lệ (0: Chủ nhật, 1: Thứ hai... 6: Thứ bảy).");

            schedule.RuleFor(s => s.StartTime)
                .NotEmpty().WithMessage("Giờ bắt đầu không được để trống.")
                .Matches(@"^([01]?[0-9]|2[0-3]):[0-5][0-9]$").WithMessage("Định dạng giờ bắt đầu không hợp lệ (HH:mm).");

            schedule.RuleFor(s => s.EndTime)
                .NotEmpty().WithMessage("Giờ kết thúc không được để trống.")
                .Matches(@"^([01]?[0-9]|2[0-3]):[0-5][0-9]$").WithMessage("Định dạng giờ kết thúc không hợp lệ (HH:mm).");
        });
    }
}
