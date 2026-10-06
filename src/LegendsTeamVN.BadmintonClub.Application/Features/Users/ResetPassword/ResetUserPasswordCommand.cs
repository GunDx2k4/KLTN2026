using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Users.ResetPassword;

public record ResetUserPasswordCommand(Guid Id, string NewPassword) : ICommand;
