using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Recruitments;

public static class RecruitmentRules
{
    public static bool OccupiesSlot(SessionBooking booking) => !booking.IsDeleted &&
        booking.Status is BookingStatus.CONFIRMED or BookingStatus.CHECKED_IN;

    public static int RemainingSlots(BadmintonSession session, GuestRecruitment recruitment)
    {
        // Reserve released slots for existing waiters; guests cannot bypass the RSVP queue.
        if (session.Bookings.Any(b => !b.IsDeleted && b.Status == BookingStatus.WAITING)) return 0;
        var sessionFree = session.MaxPlayers - session.Bookings.Count(OccupiesSlot);
        var recruitmentFree = recruitment.SlotsNeeded - session.Bookings.Count(b => b.RecruitmentId == recruitment.Id && OccupiesSlot(b));
        return Math.Max(0, Math.Min(sessionFree, recruitmentFree));
    }

    // Call while holding the session row lock, after changing bookings/capacity/status.
    public static void Synchronize(BadmintonSession session, DateTime now)
    {
        foreach (var recruitment in session.Recruitments.Where(r => !r.IsDeleted &&
                     r.Status is RecruitmentStatus.ACTIVE or RecruitmentStatus.FILLED))
        {
            if (session.IsDeleted || now >= session.EndTime || session.Status is SessionStatus.CALCULATED or SessionStatus.COMPLETED)
                recruitment.UpdateStatus(RecruitmentStatus.EXPIRED);
            else
                recruitment.UpdateStatus(session.Status == SessionStatus.OPEN && RemainingSlots(session, recruitment) > 0
                    ? RecruitmentStatus.ACTIVE : RecruitmentStatus.FILLED);
        }
    }
}
