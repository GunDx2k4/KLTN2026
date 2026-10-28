using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.FeeRule.Update;

public sealed class UpdateClubFeeRuleCommandValidator : AbstractValidator<UpdateClubFeeRuleCommand>
{
    public UpdateClubFeeRuleCommandValidator()
    {
        RuleFor(x => x.ClubId)
            .NotEmpty().WithMessage("Mã câu lạc bộ không được để trống.");

        RuleFor(x => x.Request)
            .NotNull().WithMessage("Dữ liệu cấu hình biểu phí không được để trống.");

        When(x => x.Request != null, () =>
        {
            RuleFor(x => x.Request.FeeMode)
                .IsInEnum().WithMessage("Chế độ tính phí không hợp lệ.");

            RuleFor(x => x.Request.MonthlyFee)
                .GreaterThanOrEqualTo(0).WithMessage("Hội phí tháng không được âm.");

            RuleFor(x => x.Request.DailyMemberFee)
                .GreaterThanOrEqualTo(0).WithMessage("Phí buổi cho hội viên không được âm.");

            RuleFor(x => x.Request.GuestFee)
                .GreaterThanOrEqualTo(0).WithMessage("Phí buổi cho khách vãng lai không được âm.");

            RuleFor(x => x.Request.FemaleDiscountMonthly)
                .GreaterThanOrEqualTo(0).WithMessage("Ưu đãi tháng cho Nữ không được âm.");

            RuleFor(x => x.Request.FemaleDiscountDaily)
                .GreaterThanOrEqualTo(0).WithMessage("Ưu đãi buổi cho Nữ hội viên không được âm.");

            RuleFor(x => x.Request.FemaleDiscountGuest)
                .GreaterThanOrEqualTo(0).WithMessage("Ưu đãi buổi cho Nữ khách vãng lai không được âm.");

            RuleFor(x => x.Request.RoundRule)
                .GreaterThan(0).WithMessage("Quy tắc làm tròn tiền phải lớn hơn 0.");
        });
    }
}
