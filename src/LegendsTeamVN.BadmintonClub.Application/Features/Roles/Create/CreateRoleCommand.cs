using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Roles.Create;

public record CreateRoleCommand(string Name, string? Description = null) : ICommand;
