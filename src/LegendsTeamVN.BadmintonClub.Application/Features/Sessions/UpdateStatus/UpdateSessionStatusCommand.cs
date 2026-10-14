using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.UpdateStatus;

public sealed record UpdateSessionStatusCommand(Guid SessionId, SessionStatus Status) : ICommand;
