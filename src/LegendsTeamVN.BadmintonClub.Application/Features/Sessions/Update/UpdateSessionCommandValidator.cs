using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Update;

public sealed class UpdateSessionCommandValidator : AbstractValidator<UpdateSessionCommand>
{
    public UpdateSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Mã buổi chơi không được để trống.");

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

        RuleFor(x => x.Request.MaxPlayers)
            .GreaterThan(0).WithMessage("Sĩ số tối đa phải lớn hơn 0.");
    }
}
