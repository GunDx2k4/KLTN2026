using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Join;

public record JoinClubCommand(
    string ClubCode,
    Guid? RoleId = null,
    string? Nickname = null
) : ICommand<Guid>;
