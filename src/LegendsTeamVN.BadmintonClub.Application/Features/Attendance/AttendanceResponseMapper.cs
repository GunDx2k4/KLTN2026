using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Attendance;

public static class AttendanceResponseMapper
{
    public static SessionAttendanceResponse Map(BadmintonSession session, DateTime now)
    {
        var bookings = session.Bookings.Where(b => !b.IsDeleted && b.Status != BookingStatus.ABSENT)
            .OrderBy(b => b.BookedAt).ThenBy(b => b.Id).ToList();
        var players = bookings.Select(b => new AttendancePlayerResponse(
            b.Id, b.UserId, b.User?.FullName ?? "", b.User?.AvatarUrl, b.User?.SkillLevel.ToString() ?? "NB",
            b.Status, b.CheckinAt, b.AttendanceMarkedAt, b.AttendanceMarkedBy,
            session.IsPlayerReady(b.UserId))).ToList();
        var ready = players.Count(p => p.IsReady);
        var canEdit = session.CanEditAttendance(now);
        return new SessionAttendanceResponse(session.Id, session.GroupId, session.StartTime, session.EndTime,
            session.MaxPlayers, players.Count(p => p.Status == BookingStatus.CHECKED_IN),
            players.Count(p => p.Status == BookingStatus.CONFIRMED), players.Count(p => p.Status == BookingStatus.NO_SHOW),
            players.Count(p => p.Status == BookingStatus.WAITING), ready, canEdit,
            session.CanStartRotation(now),
            session.AttendanceCompletedAt, session.AttendanceCompletedBy, players);
    }
}
