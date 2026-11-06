using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
namespace LegendsTeamVN.BadmintonClub.Application.Features.Reputation.GetReputationEntries;
public sealed record GetReputationEntriesQuery(int Page = 1, int PageSize = 20) : IQuery<ReputationPageResponse<ReputationEntryResponse>>;
