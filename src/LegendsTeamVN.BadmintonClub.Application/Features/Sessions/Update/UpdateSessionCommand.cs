using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Update;

public sealed record UpdateSessionCommand(Guid SessionId, UpdateSessionRequest Request) : ICommand;
