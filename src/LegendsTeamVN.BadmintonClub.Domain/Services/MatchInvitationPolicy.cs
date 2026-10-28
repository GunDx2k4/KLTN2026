using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Domain.Services;

public sealed record MatchInvitationRoster(Guid SessionId, Guid MatchId, int CourtNumber, DateTime? TimeStart,
    IReadOnlyList<InvitationPlayer> Players);
public sealed record InvitationPlayer(Guid UserId, string Name, SideTeam Team);
public sealed record PersonalInvitation(InvitationPlayer Teammate, IReadOnlyList<InvitationPlayer> Opponents);

public static class MatchInvitationPolicy
{
    public static PersonalInvitation ForPlayer(MatchInvitationRoster roster, Guid userId)
    {
        if (roster.Players.Count != 4 || roster.Players.Select(p => p.UserId).Distinct().Count() != 4 ||
            roster.Players.Count(p => p.Team == SideTeam.TEAM_A) != 2 || roster.Players.Count(p => p.Team == SideTeam.TEAM_B) != 2)
            throw new InvalidOperationException("Stored invitation roster is not a doubles match.");
        var self = roster.Players.Single(p => p.UserId == userId);
        return new(roster.Players.Single(p => p.UserId != userId && p.Team == self.Team),
            roster.Players.Where(p => p.Team != self.Team).ToArray());
    }
}
