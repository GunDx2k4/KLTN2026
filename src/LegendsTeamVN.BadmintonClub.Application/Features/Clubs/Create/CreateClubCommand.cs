using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Create;

public sealed record CreateClubCommand(string Name, string? Description, string? AvatarUrl) : ICommand<Guid>;
