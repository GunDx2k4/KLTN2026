using LegendsTeamVN.BadmintonClub.Domain.Entities;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Recruitments;

// Compatibility facade for existing recruitment/booking callers; invariants live in Domain.
public static class RecruitmentRules
{
    public static bool OccupiesSlot(SessionBooking booking) => booking.OccupiesSlot();

    public static int RemainingSlots(BadmintonSession session, GuestRecruitment recruitment) =>
        session.RemainingRecruitmentSlots(recruitment);

    // Call while holding the session row lock, after changing bookings/capacity/status.
    public static void Synchronize(BadmintonSession session, DateTime now) => session.SynchronizeRecruitments(now);
}
