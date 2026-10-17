using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Application.Features.Recruitments;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.UpdateStatus;

public sealed class UpdateSessionStatusCommandHandler(
    ISessionRepository sessionRepository,
    ICurrentUserService currentUser,
    TimeProvider clock,
    IGroupAuthorizationService authorization) : ICommandHandler<UpdateSessionStatusCommand>
{
    public async Task<Result> Handle(UpdateSessionStatusCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Status))
            return Result.Failure(Error.Validation("Session.InvalidStatus", "Trạng thái buổi chơi không hợp lệ."));

        if (currentUser.UserId is not Guid actorId)
            return Result.Failure(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));

        var session = await sessionRepository.GetAttendanceAsync(request.SessionId, true, cancellationToken);
        if (session is null)
        {
            return Result.Failure(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));
        }

        if (!await authorization.HasPermissionAsync(actorId, session.GroupId, GroupPermissions.Operations.SessionUpdate, cancellationToken))
            return Result.Failure(Error.Forbidden("Session.Forbidden", "Bạn không có quyền đổi trạng thái buổi chơi tại nhóm này."));

        if (!session.CanTransitionTo(request.Status))
            return Result.Failure(Error.Conflict("Session.InvalidTransition", "Không thể chuyển trạng thái buổi chơi về bước trước sau khi đã bắt đầu hoặc chốt buổi."));

        if (request.Status is SessionStatus.CALCULATED or SessionStatus.COMPLETED)
        {
            if (session.Matches.Any(m => !m.IsDeleted && m.Status == MatchStatus.PLAYING))
                return Result.Failure(Error.Conflict("Session.ActiveMatches", "Còn trận đang thi đấu. Hãy kết thúc các trận trước khi đóng buổi."));

            var notArrived = session.Bookings.Where(b => !b.IsDeleted && b.Status == BookingStatus.CONFIRMED).ToList();
            if (notArrived.Count > 0)
            {
                if (!request.ConfirmRemainingNoShows)
                    return Result.Failure(Error.Conflict("Attendance.NoShowConfirmationRequired",
                        $"Còn {notArrived.Count} người đã đăng ký nhưng chưa có mặt. Hãy kiểm tra danh sách điểm danh và xác nhận đánh dấu NO_SHOW trước khi đóng buổi."));

                var now = clock.GetUtcNow().UtcDateTime;
                foreach (var booking in notArrived)
                    booking.MarkNoShow(actorId, now);
            }
        }

        session.UpdateStatus(request.Status);
        RecruitmentRules.Synchronize(session, clock.GetUtcNow().UtcDateTime);
        sessionRepository.Update(session);

        return Result.Success();
    }
}
