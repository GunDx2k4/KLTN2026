using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Users.UpdateRoles;

public record UpdateUserRolesCommand(Guid Id, List<string> Roles) : ICommand;
