using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.ProcessDue;

// Internal scheduler command. There is deliberately no HTTP endpoint for this operation.
public sealed record ProcessDueRotationCommand(Guid SessionId, bool Recover = false) : ICommand;
