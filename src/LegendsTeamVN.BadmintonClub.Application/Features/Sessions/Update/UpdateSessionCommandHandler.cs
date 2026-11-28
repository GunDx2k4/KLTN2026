using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Application.Features.Payments.Advance;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Application.Features.Recruitments;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Update;

public sealed class UpdateSessionCommandHandler(ISessionRepository sessionRepository, TimeProvider? clock = null, AdvancePaymentConfiguration? advances = null) : ICommandHandler<UpdateSessionCommand>
{
    public async Task<Result> Handle(UpdateSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await sessionRepository.GetAttendanceAsync(request.SessionId, true, cancellationToken);
        if (session is null)
        {
            return Result.Failure(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));
        }

        var req = request.Request;
        if (session.MinimumPlayers is null && (req.MinimumPlayers.HasValue || req.ShortageCheckMinutes.HasValue) &&
            req.EndTime <= (clock ?? TimeProvider.System).GetUtcNow())
            return Result.Failure(Error.Conflict("Session.Closed", "Buổi đã hết giờ, không thể bật nhắc thiếu người."));
        if (session.RotationStartedAt.HasValue)
            return Result.Failure(Error.Conflict("Rotation.ParametersFrozen", "Không sửa thông số buổi sau khi bắt đầu điều phối."));
        if (session.Status is SessionStatus.CALCULATED or SessionStatus.COMPLETED or SessionStatus.NOT_HELD)
            return Result.Failure(Error.Conflict("Session.Closed", "Buổi đã chốt chi phí hoặc kết thúc, không thể sửa thông số vận hành."));
        if (req.MaxPlayers < session.Bookings.Count(b => !b.IsDeleted && b.Status is BookingStatus.CONFIRMED or BookingStatus.CHECKED_IN))
            return Result.Failure(Error.Conflict("Session.CapacityTooSmall", "Sức chứa không được thấp hơn số người đã giữ chỗ chính thức hoặc có mặt."));
        var configured = session.ConfigureShortage(req.MinimumPlayers, req.ShortageCheckMinutes, req.MaxPlayers,
            session.StartTime != req.StartTime.UtcDateTime);
        if (configured.IsFailure) return configured;
        if (req.AdvanceEnabled is bool enabled)
        {
            if (advances is null) throw new InvalidOperationException("Thiếu cấu hình khoản ứng.");
            var advanceSetting = await advances.ConfigureAsync(session, enabled, req.AdvanceAmount, cancellationToken);
            if (advanceSetting.IsFailure) return advanceSetting;
        }
        session.UpdateSession(
            title: req.Title,
            startTime: req.StartTime.UtcDateTime,
            endTime: req.EndTime.UtcDateTime,
            courtLocation: req.CourtLocation,
            courtCount: req.CourtCount,
            maxPlayers: req.MaxPlayers,
            deadlineBooking: req.DeadlineBooking.UtcDateTime
        );

        session.ScheduleShortageCheck();
        RecruitmentRules.Synchronize(session, DateTime.UtcNow);

        sessionRepository.Update(session);

        return Result.Success();
    }
}
