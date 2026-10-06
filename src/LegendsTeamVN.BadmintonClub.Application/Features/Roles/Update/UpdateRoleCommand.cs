using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Roles.Update;

public record UpdateRoleCommand(Guid Id, string Name, string? Description = null) : ICommand;
