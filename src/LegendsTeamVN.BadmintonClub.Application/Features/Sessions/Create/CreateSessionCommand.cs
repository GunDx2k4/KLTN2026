using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Create;

public sealed record CreateSessionCommand(Guid ClubId, CreateSessionRequest Request) : ICommand<Guid>;
