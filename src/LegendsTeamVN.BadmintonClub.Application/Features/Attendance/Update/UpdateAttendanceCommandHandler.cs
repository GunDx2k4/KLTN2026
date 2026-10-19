using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Attendance.Update;

public sealed class UpdateAttendanceCommandHandler(AttendanceService attendance)
    : ICommandHandler<UpdateAttendanceCommand, SessionAttendanceResponse>
{
    public Task<Result<SessionAttendanceResponse>> Handle(UpdateAttendanceCommand request, CancellationToken cancellationToken) =>
        attendance.UpdateAsync(request.SessionId, request.UserId, request.Status, cancellationToken);
}
