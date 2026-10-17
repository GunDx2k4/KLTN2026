using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Attendance.Complete;

public sealed record CompleteAttendanceCommand(Guid SessionId) : ICommand<SessionAttendanceResponse>;
