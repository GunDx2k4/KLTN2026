using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.Reconciliation.List;

public sealed class ListReconciliationsQueryValidator : AbstractValidator<ListReconciliationsQuery>
{
    public ListReconciliationsQueryValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty().WithMessage("Nhóm không hợp lệ.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("Số bản ghi mỗi trang phải từ 1 đến 100.");
        RuleFor(x => x.Page).InclusiveBetween(1, int.MaxValue / 100).WithMessage("Số trang vượt phạm vi cho phép.");
        RuleFor(x => x.Status).IsInEnum().WithMessage("Trạng thái đối soát không hợp lệ.").When(x => x.Status.HasValue);
    }
}
