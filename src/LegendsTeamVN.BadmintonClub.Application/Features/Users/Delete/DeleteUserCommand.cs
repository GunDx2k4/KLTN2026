using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Users.Delete;

public record DeleteUserCommand(Guid Id) : ICommand;
