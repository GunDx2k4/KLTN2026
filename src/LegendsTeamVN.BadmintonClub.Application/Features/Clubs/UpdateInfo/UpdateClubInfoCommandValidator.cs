using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.UpdateInfo;

public sealed class UpdateClubInfoCommandValidator : AbstractValidator<UpdateClubInfoCommand>
{
    public UpdateClubInfoCommandValidator()
    {
        RuleFor(x => x.ClubId)
            .NotEmpty().WithMessage("ID câu lạc bộ không được để trống.");

        RuleFor(x => x.Request.GroupName)
            .NotEmpty().WithMessage("Tên câu lạc bộ không được để trống.")
            .MaximumLength(150).WithMessage("Tên câu lạc bộ không được vượt quá 150 ký tự.");

        RuleFor(x => x.Request.MinSkillLevel)
            .IsInEnum().WithMessage("Trình độ tối thiểu không hợp lệ.");

        RuleFor(x => x.Request.MaxSkillLevel)
            .IsInEnum().WithMessage("Trình độ tối đa không hợp lệ.");

        RuleFor(x => x.Request)
            .Must(r => r.MaxSkillLevel >= r.MinSkillLevel)
            .WithMessage("Trình độ tối đa phải lớn hơn hoặc bằng trình độ tối thiểu.");

        When(x => x.Request.Lat.HasValue || x.Request.Lng.HasValue, () =>
        {
            RuleFor(x => x.Request.Lat)
                .NotNull().WithMessage("Vui lòng cung cấp đầy đủ vĩ độ (latitude).")
                .InclusiveBetween(-90, 90).WithMessage("Vĩ độ không hợp lệ.");

            RuleFor(x => x.Request.Lng)
                .NotNull().WithMessage("Vui lòng cung cấp đầy đủ kinh độ (longitude).")
                .InclusiveBetween(-180, 180).WithMessage("Kinh độ không hợp lệ.");
        });
    }
}
