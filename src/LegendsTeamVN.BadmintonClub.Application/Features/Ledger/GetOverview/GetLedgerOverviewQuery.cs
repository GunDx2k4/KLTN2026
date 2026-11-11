using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger.GetOverview;

public sealed record GetLedgerOverviewQuery(Guid ClubId, LedgerReadFilter Filter) : IQuery<LedgerOverviewResponse>;
