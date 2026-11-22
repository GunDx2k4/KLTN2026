using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.PassCourt;

public sealed record CreateCourtPassCommand(CreateCourtPassRequest Request) : ICommand<Guid>;
