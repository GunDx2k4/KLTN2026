using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Users.Create;

public record CreateUserCommand(string Email, string Password, List<string>? Roles = null) : ICommand<Guid>;
