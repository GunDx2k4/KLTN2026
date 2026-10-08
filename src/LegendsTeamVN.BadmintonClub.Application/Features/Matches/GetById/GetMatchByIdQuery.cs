using LegendsTeamVN.BadmintonClub.Application.DTOs.Matches.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Matches.GetById;

// UserId is resolved by the authenticated endpoint or hub, never bound from client input.
public sealed record GetMatchByIdQuery(Guid Id, Guid UserId) : IQuery<MatchResponse>;
