using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Join;

public record JoinClubCommand(
    string ClubCode,
    ClubRole Role = ClubRole.Member,
    string? Nickname = null
) : ICommand<Guid>;
