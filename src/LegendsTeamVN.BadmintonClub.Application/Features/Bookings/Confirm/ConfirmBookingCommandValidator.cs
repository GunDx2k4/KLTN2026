using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Bookings.Confirm;

public sealed class ConfirmBookingCommandValidator : AbstractValidator<ConfirmBookingCommand>
{
    public ConfirmBookingCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("Mã buổi chơi không được để trống.");
        RuleFor(x => x.BookingId).NotEmpty().WithMessage("Mã đăng ký giữ chỗ không được để trống.");
    }
}
