using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Delete;

public sealed record DeleteSessionCommand(Guid SessionId) : ICommand;
