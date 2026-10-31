using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Services;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation;

public static class RotationResponseMapper
{
    public static RotationResponse Map(BadmintonSession session, DateTime now, bool canEditManually = false, bool canResumePaused = false)
    {
        var active = session.Matches.Where(m => !m.IsDeleted && m.Status is MatchStatus.WAITING or MatchStatus.PLAYING).ToList();
        RotationPlayerResponse Player(SessionBooking b, SideTeam? team = null)
        {
            var assigned = active.FirstOrDefault(m => m.Players.Any(p => p.UserId == b.UserId));
            var lastEnd = session.Matches.Where(m => !m.IsDeleted && m.Status == MatchStatus.FINISHED &&
                m.Players.Any(p => p.UserId == b.UserId)).Select(m => m.TimeEnd).Max();
            var state = assigned?.Status switch
            {
                MatchStatus.WAITING => "Proposed", MatchStatus.PLAYING => "Playing",
                _ => b.IsRotationPaused ? "Paused" : lastEnd.HasValue ? "Resting" : "Ready"
            };
            var readyAt = MatchRotationEngine.EligibleAgainAt(session, b.UserId);
            return new(b.UserId, b.User?.FullName ?? "", b.User?.SkillLevel ?? SkillLevel.NB, team, state, b.CheckinAt, lastEnd,
                readyAt, assigned is null && readyAt > now,
                session.CanEditAttendance(now) && session.StartTime <= now && session.IsPlayerReady(b.UserId, now),
                false, b.IsRotationPaused,
                canEditManually && !b.IsRotationPaused && assigned?.Status != MatchStatus.PLAYING &&
                    session.RotationStartedAt.HasValue && session.CanEditAttendance(now) && session.StartTime <= now,
                canResumePaused && b.IsRotationPaused && !session.IsDeleted,
                PersonalMatchSchedulePolicy.PauseSource(b),
                !b.IsRotationPaused ? null : b.RotationPauseSource == RotationPauseSource.Self ? "AutoAfterNextStartedMatchFinishes" : "HostOnly",
                b.SelfPauseTargetMatchId,
                b.RotationPauseSource == RotationPauseSource.Self && b.IsRotationPaused
                    ? b.SelfPauseTargetMatchId.HasValue ? "AwaitingTargetFinish" : "AwaitingNextStart" : null);
        }
        RotationMatchResponse Match(SessionMatch m)
        {
            var skillsA = m.Players.Where(p => p.SideTeam == SideTeam.TEAM_A)
                .Select(p => session.Bookings.FirstOrDefault(b => b.UserId == p.UserId)?.User?.SkillLevel ?? SkillLevel.NB).ToArray();
            var skillsB = m.Players.Where(p => p.SideTeam == SideTeam.TEAM_B)
                .Select(p => session.Bookings.FirstOrDefault(b => b.UserId == p.UserId)?.User?.SkillLevel ?? SkillLevel.NB).ToArray();
            var score = skillsA.Length == 2 && skillsB.Length == 2 ? MatchRotationEngine.CalculateSkillBalance(skillsA, skillsB) : default;
            return new(m.Id, m.CourtNumber, m.RoundNumber, m.Status,
            m.TimeStart, m.TimeEnd, m.Players.Select(p =>
            {
                var booking = session.Bookings.FirstOrDefault(b => b.UserId == p.UserId);
                return booking is not null ? Player(booking, p.SideTeam) with { IsManualSwapped = p.IsManualSwapped } :
                    new RotationPlayerResponse(p.UserId, "", SkillLevel.NB, p.SideTeam,
                        m.Status == MatchStatus.PLAYING ? "Playing" : "Proposed", null, null, null, false, false);
            }).ToList(), m.OriginalLineupSnapshot is { } original ? new(original.SchemaVersion, original.TeamA.ToArray(), original.TeamB.ToArray()) : null,
                score.TeamATotal, score.TeamBTotal, score.Difference, m.OriginalLineupSnapshot is not null,
                canEditManually && m.Status == MatchStatus.WAITING && session.RotationStartedAt.HasValue &&
                    session.CanEditAttendance(now) && session.StartTime <= now,
                canEditManually && m.OriginalLineupSnapshot is not null && m.Status == MatchStatus.WAITING &&
                    session.CanEditAttendance(now) && session.StartTime <= now,
                canEditManually && m.Status == MatchStatus.WAITING && session.RotationStartedAt.HasValue &&
                    session.CanEditAttendance(now) && session.StartTime <= now);
        }
        var available = MatchRotationEngine.Candidates(session, now).Count;
        var unassigned = MatchRotationEngine.Unassigned(session).Count;
        var open = session.CanEditAttendance(now) && session.StartTime <= now;
        var canGenerate = session.RotationStartedAt.HasValue && session.CanEditAttendance(now) &&
            session.StartTime <= now && session.HasFreeCourt() && available >= 4;
        return new(session.Id, session.Status, session.RotationVersion, now, session.RotationStartedAt,
            session.CanStartRotation(now) && session.HasFreeCourt(), canGenerate,
            open && session.HasFreeCourt() && available < 4 ? (unassigned >= 4 ? "PlayersCoolingDown" : "NotEnoughPlayers") : null,
            Enumerable.Range(1, session.CourtCount).Select(c =>
            {
                var m = active.FirstOrDefault(x => x.CourtNumber == c);
                return new RotationCourtResponse(c, m is null ? "Empty" : m.Status == MatchStatus.WAITING ? "Proposed" : "Playing", m is null ? null : Match(m));
            }).ToList(), session.Bookings.Where(MatchRotationEngine.IsCheckedInParticipant).Select(b => Player(b)).ToList(),
            MatchRotationEngine.MinRestMinutes, session.NextProposalCheckAt, available, unassigned - available);
    }
}
