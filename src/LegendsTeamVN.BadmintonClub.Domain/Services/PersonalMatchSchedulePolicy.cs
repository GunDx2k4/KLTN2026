using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Domain.Services;

public static class PersonalMatchSchedulePolicy
{
    public static PersonalMatchSchedule Evaluate(BadmintonSession session, Guid userId, DateTime now,
        bool rotationEnabled, bool personalEnabled, bool selfPauseEnabled)
    {
        session.ValidateRotationPauseMetadata();
        var booking = session.Bookings.Single(b => b.UserId == userId && !b.IsDeleted);
        var closed = session.IsDeleted || now >= session.EndTime ||
            session.Status is SessionStatus.CALCULATED or SessionStatus.COMPLETED;
        var phase = closed ? "Closed" : !session.RotationStartedAt.HasValue || now < session.StartTime ? "AwaitingRotation" : "Active";
        var current = session.Matches.SingleOrDefault(m => !m.IsDeleted && m.Status == MatchStatus.PLAYING && m.Players.Any(p => p.UserId == userId));
        var next = current is null ? session.Matches.SingleOrDefault(m => !m.IsDeleted && m.Status == MatchStatus.WAITING && m.Players.Any(p => p.UserId == userId)) : null;
        var lastEnd = MatchRotationEngine.LastMatchEndAt(session, userId);
        var state = current is not null ? "Playing" : next is not null ? "Proposed" : booking.IsRotationPaused ? "Paused" : lastEnd.HasValue ? "Resting" : "Ready";
        var restStart = current is not null ? null : lastEnd ?? booking.CheckinAt;
        var eligible = MatchRotationEngine.EligibleAgainAt(session, userId);
        var cooldown = eligible.HasValue ? (long)Math.Max(0, Math.Ceiling((eligible.Value - now).TotalSeconds)) : 0;
        var candidates = MatchRotationEngine.Candidates(session, now);
        var index = candidates.ToList().FindIndex(b => b.UserId == userId);
        int? priority = phase == "Active" && index >= 0 ? index + 1 : null;
        var reason = state is "Playing" or "Proposed" ? state : phase != "Active" ? phase :
            booking.IsRotationPaused ? "Paused" : cooldown > 0 ? "CoolingDown" : "Ready";
        var estimate = reason == "Proposed" ? "AwaitingHostStart" : reason != "Ready" ? reason :
            !session.HasFreeCourt() ? "AwaitingCourt" : candidates.Count < 4 ? "NotEnoughPlayers" : "NoProposal";
        var source = PauseSource(booking);
        return new(phase, state, current, next, restStart,
            restStart.HasValue ? (long)Math.Max(0, Math.Floor((now - restStart.Value).TotalSeconds)) : null,
            eligible, cooldown, priority, reason, estimate,
            personalEnabled && rotationEnabled && selfPauseEnabled && phase == "Active" && current is null && !booking.IsRotationPaused,
            personalEnabled && phase == "Active" && current is null && source == "Self", source,
            source == "Self" ? "AutoAfterNextStartedMatchFinishes" : source is null ? null : "HostOnly",
            booking.SelfPauseTargetMatchId,
            source == "Self" ? booking.SelfPauseTargetMatchId.HasValue ? "AwaitingTargetFinish" : "AwaitingNextStart" : null);
    }

    public static string? PauseSource(SessionBooking booking) => !booking.IsRotationPaused ? null :
        booking.RotationPauseSource switch { RotationPauseSource.Self => "Self", RotationPauseSource.Host => "Host", _ => "LegacyHost" };
}

public sealed record PersonalMatchSchedule(string Phase, string PlayerState, SessionMatch? CurrentMatch,
    SessionMatch? NextMatch, DateTime? RestStartedAt, long? RestElapsedSeconds, DateTime? EligibleAgainAt,
    long RemainingCooldownSeconds, int? QueuePriority, string AvailabilityReason, string EstimateReason,
    bool CanRequestPause, bool CanResumeSelf, string? PauseSource, string? PauseResumePolicy,
    Guid? SelfPauseTargetMatchId, string? SelfPauseProgress);
