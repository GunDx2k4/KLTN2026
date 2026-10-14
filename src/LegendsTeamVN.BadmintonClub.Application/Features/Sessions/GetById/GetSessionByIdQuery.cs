using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.GetById;

public sealed record GetSessionByIdQuery(Guid SessionId) : IQuery<SessionDetailResponse>;
