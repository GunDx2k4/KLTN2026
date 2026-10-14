using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Recruitments.Create;

public sealed class CreateRecruitmentCommandValidator : AbstractValidator<CreateRecruitmentCommand>
{
    public CreateRecruitmentCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("Mã buổi chơi không được để trống.");

        RuleFor(x => x.Request.SlotsNeeded)
            .GreaterThan(0).WithMessage("Số lượng suất cần tìm phải lớn hơn 0.");

        RuleFor(x => x.Request.ScanRadiusKm)
            .GreaterThan(0).WithMessage("Bán kính tìm kiếm phải lớn hơn 0.");

        RuleFor(x => x.Request.SharedCost)
            .GreaterThanOrEqualTo(0).WithMessage("Chi phí chia sẻ không được âm.");
    }
}
