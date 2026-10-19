using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Attendance.Update;

public sealed record UpdateAttendanceCommand(Guid SessionId, Guid UserId, BookingStatus Status) : ICommand<SessionAttendanceResponse>;
