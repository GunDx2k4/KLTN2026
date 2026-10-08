using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Matches.Rsvp;

public sealed record RsvpDecision(MatchPlayerStatus? Status, bool ResetJoinedAt = false);

public static class MatchRsvpRules
{
    public static Result<RsvpDecision> Decide(Match match, DateTimeOffset timeStart, IReadOnlyCollection<MatchPlayer> players,
        Guid userId, bool withdraw, bool joinWaitlist, DateTimeOffset now)
    {
        if (match.Status is not (MatchStatus.Open or MatchStatus.Full))
            return Result.Failure<RsvpDecision>(Error.Conflict("Match.NotOpen", "This match is not accepting RSVPs."));
        if (now >= match.RegistrationClosesAt || now >= timeStart)
            return Result.Failure<RsvpDecision>(Error.Conflict("Match.RegistrationClosed", "Registration is closed. Please contact the Host."));
        if (match.MaxPlayers <= 0 || match.RegistrationClosesAt > timeStart)
            return Result.Failure<RsvpDecision>(Error.Conflict("Match.InvalidRegistration", "The match registration settings are invalid. Please contact the Host."));

        var active = players.Where(x => !x.IsDeleted).ToList();
        var player = active.SingleOrDefault(x => x.UserId == userId);
        if (withdraw)
            return Result.Success(new RsvpDecision(player is null || player.Status == MatchPlayerStatus.Cancelled
                ? null : MatchPlayerStatus.Cancelled));

        if (player?.Status is MatchPlayerStatus.Approved or MatchPlayerStatus.Waitlisted)
            return Result.Success(new RsvpDecision(null));

        var mustWait = active.Count(x => x.Status == MatchPlayerStatus.Approved) >= match.MaxPlayers
            || active.Any(x => x.Status == MatchPlayerStatus.Waitlisted);
        if (mustWait && !joinWaitlist)
            return Result.Failure<RsvpDecision>(Error.Conflict("Match.WaitlistConfirmationRequired",
                "A confirmed place is unavailable. Would you like to join the waitlist?"));

        return Result.Success(new RsvpDecision(mustWait ? MatchPlayerStatus.Waitlisted : MatchPlayerStatus.Approved, true));
    }
}
