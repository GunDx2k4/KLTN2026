using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Users.ToggleLock;

public record ToggleUserLockCommand(Guid Id, bool IsLocked) : ICommand;
