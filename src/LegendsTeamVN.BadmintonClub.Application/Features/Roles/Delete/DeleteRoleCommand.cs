using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Roles.Delete;

public record DeleteRoleCommand(Guid Id) : ICommand;
