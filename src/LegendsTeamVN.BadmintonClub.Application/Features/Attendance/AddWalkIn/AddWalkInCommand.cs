using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Attendance.AddWalkIn;

public sealed record AddWalkInCommand(Guid SessionId, Guid UserId) : ICommand<SessionAttendanceResponse>;
