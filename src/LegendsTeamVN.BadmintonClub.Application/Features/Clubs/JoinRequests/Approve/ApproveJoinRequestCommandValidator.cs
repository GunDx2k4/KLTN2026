using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.JoinRequests.Approve;

public sealed class ApproveJoinRequestCommandValidator : AbstractValidator<ApproveJoinRequestCommand>
{
    public ApproveJoinRequestCommandValidator()
    {
        RuleFor(x => x.ClubId)
            .NotEmpty().WithMessage("Mã câu lạc bộ không được để trống.");

        RuleFor(x => x.RequestId)
            .NotEmpty().WithMessage("Mã yêu cầu gia nhập không được để trống.");
    }
}
