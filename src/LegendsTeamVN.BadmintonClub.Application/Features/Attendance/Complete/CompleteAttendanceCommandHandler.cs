using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Attendance.Complete;

public sealed class CompleteAttendanceCommandHandler(AttendanceService attendance)
    : ICommandHandler<CompleteAttendanceCommand, SessionAttendanceResponse>
{
    public Task<Result<SessionAttendanceResponse>> Handle(CompleteAttendanceCommand request, CancellationToken cancellationToken) =>
        attendance.CompleteAsync(request.SessionId, cancellationToken);
}
