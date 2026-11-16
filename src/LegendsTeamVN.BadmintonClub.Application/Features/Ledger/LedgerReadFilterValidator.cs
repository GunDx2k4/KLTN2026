using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Requests;
using FluentValidation;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger;

public sealed class LedgerReadFilterValidator : AbstractValidator<LedgerReadFilter>
{
    public LedgerReadFilterValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, int.MaxValue / 100).WithMessage("Số trang vượt phạm vi cho phép.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Số bản ghi mỗi trang phải từ 1 đến 100.");
        RuleFor(x => x.Type).IsInEnum().When(x => x.Type.HasValue);
        RuleFor(x => x.Category).IsInEnum().When(x => x.Category.HasValue);
        RuleFor(x => x.FromUtc).Must(x => !x.HasValue || x.Value.Kind == DateTimeKind.Utc).WithMessage("Thời điểm bắt đầu phải dùng UTC.");
        RuleFor(x => x.ToUtc).Must(x => !x.HasValue || x.Value.Kind == DateTimeKind.Utc).WithMessage("Thời điểm kết thúc phải dùng UTC.");
        RuleFor(x => x).Must(x => !x.FromUtc.HasValue || !x.ToUtc.HasValue ||
            x.FromUtc <= x.ToUtc && x.ToUtc.Value - x.FromUtc.Value <= TimeSpan.FromDays(366))
            .WithMessage("Khoảng lọc phải đúng thứ tự và không quá 366 ngày.");
        RuleFor(x => x.DebtType).Must(x => x is null or "SESSION_FEE" or "MONTHLY_FUND").WithMessage("Loại công nợ không hợp lệ.");
        RuleFor(x => x.ContributionMonth).Must(x => !x.HasValue || x.Value.Day == 1).WithMessage("Kỳ đóng phải là ngày đầu tháng.");
        RuleFor(x => x.Search).MaximumLength(100).WithMessage("Từ khóa tìm kiếm không quá 100 ký tự.");
    }
}
