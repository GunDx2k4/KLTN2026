using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Auth.Register;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
            .MaximumLength(255).WithMessage("Họ và tên không được vượt quá 255 ký tự.");

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Phải cung cấp ít nhất Email hoặc Số điện thoại để đăng ký.");

        When(x => !string.IsNullOrWhiteSpace(x.Email), () =>
        {
            RuleFor(x => x.Email)
                .EmailAddress().WithMessage("Định dạng email không hợp lệ.")
                .MaximumLength(255).WithMessage("Email không được vượt quá 255 ký tự.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber), () =>
        {
            RuleFor(x => x.PhoneNumber)
                .Matches(@"^[0-9\+\-\s]{8,20}$").WithMessage("Số điện thoại không đúng định dạng.")
                .MaximumLength(20).WithMessage("Số điện thoại không được vượt quá 20 ký tự.");
        });

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(6).WithMessage("Mật khẩu phải có ít nhất 6 ký tự.");

        RuleFor(x => x.Gender)
            .InclusiveBetween((short)0, (short)2).WithMessage("Giới tính không hợp lệ (0=Khác, 1=Nam, 2=Nữ).");

        RuleFor(x => x.SkillLevel)
            .IsInEnum().WithMessage("Trình độ cầu lông không hợp lệ.");
    }
}
