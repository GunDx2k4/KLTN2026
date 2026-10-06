using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Users.Update;

public record UpdateUserCommand(Guid Id, string Email, string? UserName, string? PhoneNumber) : ICommand;
