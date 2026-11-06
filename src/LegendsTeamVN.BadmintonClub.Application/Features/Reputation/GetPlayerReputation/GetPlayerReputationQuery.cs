using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
namespace LegendsTeamVN.BadmintonClub.Application.Features.Reputation.GetPlayerReputation;
public sealed record GetPlayerReputationQuery(Guid UserId) : IQuery<PlayerReputationResponse>;
