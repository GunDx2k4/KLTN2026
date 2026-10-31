using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Services;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Personal;

public static class MyMatchesResponseMapper
{
    public static MyMatchesResponse Map(BadmintonSession session, Guid userId, DateTime now, PersonalMatchSchedule schedule)
    {
        PersonalMatchResponse? Match(SessionMatch? match)
        {
            if (match is null) return null;
            var self = match.Players.Single(p => p.UserId == userId);
            PersonalMatchPlayerResponse Player(MatchPlayer p) => new(p.UserId,
                session.Bookings.FirstOrDefault(b => b.UserId == p.UserId)?.User?.FullName ?? "");
            return new(match.Id, match.CourtNumber, match.RoundNumber, match.Status.ToString(), match.TimeStart,
                Player(match.Players.Single(p => p.UserId != userId && p.SideTeam == self.SideTeam)),
                match.Players.Where(p => p.SideTeam != self.SideTeam).Select(Player).ToArray());
        }
        return new(session.Id, session.Status.ToString(), schedule.Phase, session.RotationVersion, now,
            schedule.PlayerState, Match(schedule.CurrentMatch), Match(schedule.NextMatch), schedule.RestStartedAt,
            schedule.RestElapsedSeconds, schedule.EligibleAgainAt, schedule.RemainingCooldownSeconds,
            schedule.QueuePriority, schedule.AvailabilityReason, null, schedule.EstimateReason,
            schedule.CanRequestPause, schedule.CanResumeSelf, schedule.PauseSource, schedule.PauseResumePolicy,
            schedule.SelfPauseTargetMatchId, schedule.SelfPauseProgress);
    }
}
