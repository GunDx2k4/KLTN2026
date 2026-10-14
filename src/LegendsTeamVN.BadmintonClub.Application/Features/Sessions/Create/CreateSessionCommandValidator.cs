using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Create;

public sealed class CreateSessionCommandValidator : AbstractValidator<CreateSessionCommand>
{
    public CreateSessionCommandValidator()
    {
        RuleFor(x => x.ClubId)
            .NotEmpty().WithMessage("Mã câu lạc bộ không được để trống.");

        RuleFor(x => x.Request.Title)
            .NotEmpty().WithMessage("Tiêu đề buổi chơi không được để trống.")
            .MaximumLength(150).WithMessage("Tiêu đề không được vượt quá 150 ký tự.");

        RuleFor(x => x.Request.StartTime)
            .NotEmpty().WithMessage("Thời gian bắt đầu không được để trống.");

        RuleFor(x => x.Request.EndTime)
            .NotEmpty().WithMessage("Thời gian kết thúc không được để trống.")
            .GreaterThan(x => x.Request.StartTime).WithMessage("Thời gian kết thúc phải sau thời gian bắt đầu.");

        RuleFor(x => x.Request.CourtLocation)
            .NotEmpty().WithMessage("Địa điểm sân không được để trống.");

        RuleFor(x => x.Request.CourtCount)
            .GreaterThan(0).WithMessage("Số lượng sân phải lớn hơn 0.");

        RuleFor(x => x.Request.TargetPlayerPerCourt)
            .GreaterThan(0).WithMessage("Định mức người trên sân phải lớn hơn 0.");
    }
}
