using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Pause;

public sealed class SetRotationPauseCommandValidator : AbstractValidator<SetRotationPauseCommand>
{
    public SetRotationPauseCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.IsPaused).NotNull();
        RuleFor(x => x.ExpectedRotationVersion).NotNull().GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReplacementUserId).Must((x, id) => id is null ||
            x.IsPaused == true && id != Guid.Empty && id != x.UserId).WithMessage("Người thay chỉ áp dụng khi tạm nghỉ và phải khác người nghỉ.");
    }
}
