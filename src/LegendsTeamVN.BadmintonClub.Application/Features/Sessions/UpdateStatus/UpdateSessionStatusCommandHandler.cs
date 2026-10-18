using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Domain.Constants;

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

        var changed = session.ChangeOperationalStatus(request.Status, request.ConfirmRemainingNoShows,
            actorId, clock.GetUtcNow().UtcDateTime);
        if (changed.IsFailure) return changed;
        sessionRepository.Update(session);

        return Result.Success();
    }
}
