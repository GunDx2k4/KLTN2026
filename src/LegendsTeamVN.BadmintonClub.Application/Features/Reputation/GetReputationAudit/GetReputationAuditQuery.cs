using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
namespace LegendsTeamVN.BadmintonClub.Application.Features.Reputation.GetReputationAudit;
public sealed record GetReputationAuditQuery(Guid ClubId, Guid SessionId, int Page = 1, int PageSize = 20) : IQuery<ReputationAuditResponse>;
