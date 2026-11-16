using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger.ListMonthlyContributionCandidates;

public sealed record ListMonthlyContributionCandidatesQuery(Guid ClubId, LedgerReadFilter Filter) : IQuery<LedgerPageResponse<MonthlyContributionCandidateResponse>>;
