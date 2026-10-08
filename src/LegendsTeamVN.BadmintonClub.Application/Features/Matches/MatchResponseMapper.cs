using LegendsTeamVN.BadmintonClub.Application.DTOs.Matches.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Matches;

internal static class MatchResponseMapper
{
    public static MatchResponse Map(Match match, Guid userId)
    {
        var players = match.MatchPlayers.Where(x => !x.IsDeleted).ToList();
        var confirmed = players.Count(x => x.Status == MatchPlayerStatus.Approved);
        var waitlist = players.Where(x => x.Status == MatchPlayerStatus.Waitlisted)
            .OrderBy(x => x.JoinedAt).ThenBy(x => x.Id).ToList();
        var position = waitlist.FindIndex(x => x.UserId == userId);
        var canRsvp = match.Status is MatchStatus.Open or MatchStatus.Full
            && match.MaxPlayers > 0
            && DateTimeOffset.UtcNow < match.RegistrationClosesAt
            && match.RegistrationClosesAt <= match.BookingDetail.TimeStart;

        return new MatchResponse(match.Id, match.HostId, match.BookingDetail.CourtId,
            match.BookingDetail.TimeStart, match.BookingDetail.TimeEnd, match.PricePerPlayer, match.Description,
            match.Status, match.MaxPlayers, confirmed, Math.Max(0, match.MaxPlayers - confirmed), waitlist.Count,
            match.RegistrationClosesAt, canRsvp, players.SingleOrDefault(x => x.UserId == userId)?.Status,
            position < 0 ? null : position + 1, match.AttendanceVersion);
    }
}
