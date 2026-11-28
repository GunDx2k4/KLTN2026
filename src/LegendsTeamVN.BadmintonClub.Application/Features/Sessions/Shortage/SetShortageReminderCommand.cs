using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Shortage;

public sealed record SetShortageReminderCommand(Guid SessionId, int? DelayMinutes) : ICommand;

public sealed class SetShortageReminderCommandHandler(ISessionRepository sessions, ISessionShortageRepository shortage,
    ICurrentUserService user, TimeProvider clock) : ICommandHandler<SetShortageReminderCommand>
{
    public async Task<Result> Handle(SetShortageReminderCommand request, CancellationToken ct)
    {
        var session = await sessions.GetAttendanceAsync(request.SessionId, true, ct);
        if (session is null)
            return Result.Failure(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));
        if (user.UserId is not Guid actor || !(await shortage.FindHostsAsync(session.GroupId, ct)).Contains(actor))
            return Result.Failure(Error.Forbidden("Session.Forbidden", "Chỉ Host của CLB có thể đổi lịch nhắc."));
        return session.SetShortageReminder(request.DelayMinutes, clock.GetUtcNow().UtcDateTime);
    }
}
