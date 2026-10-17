using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Attendance;

public static class AttendanceRules
{
    public static bool CanEdit(BadmintonSession session, DateTime now) =>
        !session.IsDeleted && session.StartTime.AddHours(-1) <= now && now < session.EndTime &&
        session.Status is SessionStatus.OPEN or SessionStatus.LOCKED or SessionStatus.PLAYING;

    public static bool OccupiesSlot(SessionBooking booking) => !booking.IsDeleted &&
        booking.Status is BookingStatus.CONFIRMED or BookingStatus.CHECKED_IN;

    public static bool HasRoom(BadmintonSession session, SessionBooking? booking) =>
        booking is not null && OccupiesSlot(booking) ||
        session.Bookings.Count(OccupiesSlot) < session.MaxPlayers;

    public static SessionAttendanceResponse Map(BadmintonSession session, DateTime now)
    {
        var assigned = session.Matches.Where(m => !m.IsDeleted && m.Status is MatchStatus.WAITING or MatchStatus.PLAYING)
            .SelectMany(m => m.Players).Select(p => p.UserId).ToHashSet();
        var bookings = session.Bookings.Where(b => !b.IsDeleted && b.Status != BookingStatus.ABSENT)
            .OrderBy(b => b.BookedAt).ThenBy(b => b.Id).ToList();
        var players = bookings.Select(b => new AttendancePlayerResponse(
            b.Id, b.UserId, b.User?.FullName ?? "", b.User?.AvatarUrl, b.User?.SkillLevel.ToString() ?? "NB",
            b.Status, b.CheckinAt, b.AttendanceMarkedAt, b.AttendanceMarkedBy,
            b.Status == BookingStatus.CHECKED_IN && !assigned.Contains(b.UserId))).ToList();
        var ready = players.Count(p => p.IsReady);
        var canEdit = CanEdit(session, now);
        return new SessionAttendanceResponse(session.Id, session.GroupId, session.StartTime, session.EndTime,
            session.MaxPlayers, players.Count(p => p.Status == BookingStatus.CHECKED_IN),
            players.Count(p => p.Status == BookingStatus.CONFIRMED), players.Count(p => p.Status == BookingStatus.NO_SHOW),
            players.Count(p => p.Status == BookingStatus.WAITING), ready, canEdit,
            canEdit && session.StartTime <= now && session.AttendanceCompletedAt.HasValue && ready >= 4,
            session.AttendanceCompletedAt, session.AttendanceCompletedBy, players);
    }
}
