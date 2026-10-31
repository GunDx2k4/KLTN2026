using System.Text;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.BadmintonClub.Domain.Services;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Personal;

public static class MatchInvitationMapper
{
    public static MatchInvitationResponse Map(RotationChangedDomainEvent change, MatchInvitationRoster roster, Guid userId)
    {
        var personal = MatchInvitationPolicy.ForPlayer(roster, userId);
        PersonalMatchPlayerResponse Player(InvitationPlayer player) => new(player.UserId,
            string.Concat(player.Name.EnumerateRunes().Take(40).Select(r => r.ToString())));
        return new(change.Id, change.SessionId, roster.MatchId, roster.CourtNumber,
            roster.TimeStart ?? change.OccurredAt, change.Version, Player(personal.Teammate), personal.Opponents.Select(Player).ToArray());
    }

    public static string Body(MatchInvitationResponse invitation) =>
        $"Sân {invitation.CourtNumber}. Bạn cặp: {invitation.Teammate.Name}. Đối thủ: {string.Join(", ", invitation.Opponents.Select(p => p.Name))}.";
}
