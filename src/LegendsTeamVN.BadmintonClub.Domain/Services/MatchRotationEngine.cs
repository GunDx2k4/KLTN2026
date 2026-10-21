using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Domain.Services;

public static class MatchRotationEngine
{
    public const int MinRestMinutes = 5;

    public static bool IsEligible(SessionBooking booking) => !booking.IsRotationPaused && IsCheckedInParticipant(booking);

    public static bool IsCheckedInParticipant(SessionBooking booking) => !booking.IsDeleted &&
        booking.Status == BookingStatus.CHECKED_IN &&
        (booking.User is null || (booking.User.IsActive && !booking.User.IsDeleted));

    public static DateTime? LastMatchEndAt(BadmintonSession session, Guid userId) => session.Matches
        .Where(m => !m.IsDeleted && m.Status == MatchStatus.FINISHED && m.TimeEnd.HasValue &&
            m.Players.Any(p => p.UserId == userId)).Select(m => m.TimeEnd).Max();

    public static DateTime? EligibleAgainAt(BadmintonSession session, Guid userId) =>
        LastMatchEndAt(session, userId)?.AddMinutes(MinRestMinutes);

    public static bool HasRested(BadmintonSession session, Guid userId, DateTime now) =>
        EligibleAgainAt(session, userId) is not DateTime readyAt || now >= readyAt;

    public static IReadOnlyList<SessionBooking> Unassigned(BadmintonSession session)
    {
        var assigned = session.Matches.Where(m => !m.IsDeleted && m.Status is MatchStatus.WAITING or MatchStatus.PLAYING)
            .SelectMany(m => m.Players).Select(p => p.UserId).ToHashSet();
        return session.Bookings.Where(b => IsEligible(b) && !assigned.Contains(b.UserId)).ToList();
    }

    public static DateTime? NextCandidateTime(BadmintonSession session, DateTime now)
    {
        var times = Unassigned(session).Select(b => EligibleAgainAt(session, b.UserId) ?? now)
            .OrderBy(t => t).Take(4).ToList();
        return times.Count == 4 ? (times[3] < now ? now : times[3]) : null;
    }

    public static IReadOnlyList<SessionBooking> Candidates(BadmintonSession session, DateTime now)
    {
        var lastEnds = session.Matches.Where(m => !m.IsDeleted && m.Status == MatchStatus.FINISHED && m.TimeEnd.HasValue)
            .SelectMany(m => m.Players.Select(p => (p.UserId, End: m.TimeEnd!.Value)))
            .GroupBy(p => p.UserId).ToDictionary(g => g.Key, g => g.Max(p => p.End));
        return Unassigned(session).Where(b => !lastEnds.TryGetValue(b.UserId, out var end) || now >= end.AddMinutes(MinRestMinutes))
            .OrderBy(b => lastEnds.ContainsKey(b.UserId) ? 1 : 0)
            .ThenBy(b => lastEnds.GetValueOrDefault(b.UserId, DateTime.MinValue))
            .ThenBy(b => b.CheckinAt ?? b.AttendanceMarkedAt ?? b.BookedAt).ThenBy(b => b.UserId).ToList();
    }

    public static (int TeamATotal, int TeamBTotal, int Difference) CalculateSkillBalance(
        IReadOnlyCollection<SkillLevel> teamA, IReadOnlyCollection<SkillLevel> teamB)
    {
        ArgumentNullException.ThrowIfNull(teamA);
        ArgumentNullException.ThrowIfNull(teamB);
        if (teamA.Count != 2 || teamB.Count != 2)
            throw new ArgumentException("Each doubles team requires exactly two skill levels.");

        var totalA = teamA.Sum(skill => (int)skill);
        var totalB = teamB.Sum(skill => (int)skill);
        return (totalA, totalB, Math.Abs(totalA - totalB));
    }

    // The first two players form TEAM_A, the final two TEAM_B. Inputs are already priority ordered.
    public static IReadOnlyList<SessionBooking> Balance(IReadOnlyList<SessionBooking> selected)
    {
        if (selected.Count != 4) throw new ArgumentException("Exactly four players are required.", nameof(selected));
        var p = selected.OrderBy(b => b.UserId).ToArray();
        SkillLevel Score(int i) => p[i].User?.SkillLevel ?? SkillLevel.NB;
        var pairings = new[] { new[] { 0, 1, 2, 3 }, new[] { 0, 2, 1, 3 }, new[] { 0, 3, 1, 2 } };
        var best = pairings.OrderBy(x => CalculateSkillBalance(
            [Score(x[0]), Score(x[1])], [Score(x[2]), Score(x[3])]).Difference).First();
        return best.Select(i => p[i]).ToList();
    }
}
