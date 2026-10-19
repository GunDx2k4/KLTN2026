using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Attendance.GetList;

public sealed class GetSessionAttendanceQueryHandler(AttendanceService attendance)
    : IQueryHandler<GetSessionAttendanceQuery, SessionAttendanceResponse>
{
    public Task<Result<SessionAttendanceResponse>> Handle(GetSessionAttendanceQuery request, CancellationToken cancellationToken) =>
        attendance.GetAsync(request.SessionId, cancellationToken);
}
