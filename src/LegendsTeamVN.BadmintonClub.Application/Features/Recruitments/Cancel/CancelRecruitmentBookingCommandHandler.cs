using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Recruitments.Cancel;

public sealed class CancelRecruitmentBookingCommandHandler(
    IRecruitmentRepository recruitmentRepository,
    ISessionRepository sessionRepository,
    IBookingRepository bookingRepository,
    ICurrentUserService currentUserService,
    TimeProvider clock) : ICommandHandler<CancelRecruitmentBookingCommand>
{
    public async Task<Result> Handle(CancelRecruitmentBookingCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập để hủy suất giao lưu."));
        }

        var recruitment = await recruitmentRepository.FindAll(r => r.Id == request.RecruitmentId && !r.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
        if (recruitment is null)
        {
            return Result.Failure(Error.NotFound("Recruitment.NotFound", "Không tìm thấy bài đăng tìm khách giao lưu."));
        }

        var session = await sessionRepository.GetAttendanceAsync(recruitment.SessionId, true, cancellationToken);
        if (session is null)
            return Result.Failure(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));
        var now = clock.GetUtcNow().UtcDateTime;
        if (session.Status != SessionStatus.OPEN || now >= session.StartTime)
            return Result.Failure(Error.Conflict("Attendance.ManagedByHost", "Buổi chơi đã bắt đầu. Hãy liên hệ Host hoặc Thủ quỹ để thay đổi điểm danh."));
        var booking = session.Bookings.FirstOrDefault(b => b.RecruitmentId == request.RecruitmentId && b.UserId == userId.Value && b.Status == BookingStatus.CONFIRMED);

        if (booking is null)
        {
            return Result.Failure(Error.NotFound("Booking.NotFound", "Bạn không có đăng ký giữ chỗ qua bài đăng giao lưu này."));
        }

        booking.CancelWithEvidence(session, userId.Value,
            Domain.Services.ReputationCancellationSource.Recruitment, now);
        bookingRepository.Update(booking);

        RecruitmentRules.Synchronize(session, now);

        return Result.Success();
    }
}
