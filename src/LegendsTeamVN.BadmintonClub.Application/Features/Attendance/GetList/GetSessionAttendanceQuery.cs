using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Attendance.GetList;

public sealed record GetSessionAttendanceQuery(Guid SessionId) : IQuery<SessionAttendanceResponse>;
