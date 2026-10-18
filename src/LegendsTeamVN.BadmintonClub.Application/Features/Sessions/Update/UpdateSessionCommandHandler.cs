using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Application.Features.Recruitments;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Update;

public sealed class UpdateSessionCommandHandler(ISessionRepository sessionRepository) : ICommandHandler<UpdateSessionCommand>
{
    public async Task<Result> Handle(UpdateSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await sessionRepository.GetAttendanceAsync(request.SessionId, true, cancellationToken);
        if (session is null)
        {
            return Result.Failure(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));
        }

        var req = request.Request;
        if (session.RotationStartedAt.HasValue)
            return Result.Failure(Error.Conflict("Rotation.ParametersFrozen", "Không sửa thông số buổi sau khi bắt đầu điều phối."));
        if (session.Status is SessionStatus.CALCULATED or SessionStatus.COMPLETED)
            return Result.Failure(Error.Conflict("Session.Closed", "Buổi đã chốt chi phí hoặc kết thúc, không thể sửa thông số vận hành."));
        if (req.MaxPlayers < session.Bookings.Count(b => !b.IsDeleted && b.Status is BookingStatus.CONFIRMED or BookingStatus.CHECKED_IN))
            return Result.Failure(Error.Conflict("Session.CapacityTooSmall", "Sức chứa không được thấp hơn số người đã giữ chỗ chính thức hoặc có mặt."));
        session.UpdateSession(
            title: req.Title,
            startTime: req.StartTime.UtcDateTime,
            endTime: req.EndTime.UtcDateTime,
            courtLocation: req.CourtLocation,
            courtCount: req.CourtCount,
            maxPlayers: req.MaxPlayers,
            deadlineBooking: req.DeadlineBooking.UtcDateTime
        );

        RecruitmentRules.Synchronize(session, DateTime.UtcNow);

        sessionRepository.Update(session);

        return Result.Success();
    }
}
