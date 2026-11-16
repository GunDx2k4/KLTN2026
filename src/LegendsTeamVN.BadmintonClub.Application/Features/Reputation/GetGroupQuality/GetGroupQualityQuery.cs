using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
namespace LegendsTeamVN.BadmintonClub.Application.Features.Reputation.GetGroupQuality;
public sealed record GetGroupQualityQuery(Guid GroupId) : IQuery<GroupQualityResponse>;
