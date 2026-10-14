using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Recruitments.Cancel;

public sealed class CancelRecruitmentBookingCommandHandler(
    IRecruitmentRepository recruitmentRepository,
    IBookingRepository bookingRepository,
    ICurrentUserService currentUserService) : ICommandHandler<CancelRecruitmentBookingCommand>
{
    public async Task<Result> Handle(CancelRecruitmentBookingCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập để hủy suất giao lưu."));
        }

        var recruitment = await recruitmentRepository.FindByIdAsync(request.RecruitmentId, cancellationToken);
        if (recruitment is null)
        {
            return Result.Failure(Error.NotFound("Recruitment.NotFound", "Không tìm thấy bài đăng tìm khách giao lưu."));
        }

        var booking = await bookingRepository.FindAll(b => b.RecruitmentId == request.RecruitmentId && b.UserId == userId.Value && b.Status == BookingStatus.CONFIRMED)
            .FirstOrDefaultAsync(cancellationToken);

        if (booking is null)
        {
            return Result.Failure(Error.NotFound("Booking.NotFound", "Bạn không có đăng ký giữ chỗ qua bài đăng giao lưu này."));
        }

        booking.UpdateStatus(BookingStatus.ABSENT);
        bookingRepository.Update(booking);

        // Mở lại trạng thái ACTIVE nếu trước đó đã bị FILLED
        if (recruitment.Status == RecruitmentStatus.FILLED)
        {
            recruitment.UpdateStatus(RecruitmentStatus.ACTIVE);
            recruitmentRepository.Update(recruitment);
        }

        return Result.Success();
    }
}
