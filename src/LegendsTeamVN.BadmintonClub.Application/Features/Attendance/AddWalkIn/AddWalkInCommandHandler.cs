using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Attendance.AddWalkIn;

public sealed class AddWalkInCommandHandler(AttendanceService attendance)
    : ICommandHandler<AddWalkInCommand, SessionAttendanceResponse>
{
    public Task<Result<SessionAttendanceResponse>> Handle(AddWalkInCommand request, CancellationToken cancellationToken) =>
        attendance.AddWalkInAsync(request.SessionId, request.UserId, cancellationToken);
}
