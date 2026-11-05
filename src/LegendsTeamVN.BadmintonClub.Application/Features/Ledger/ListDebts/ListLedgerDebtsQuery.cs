using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger.ListDebts;

public sealed record ListLedgerDebtsQuery(Guid ClubId, LedgerReadFilter Filter) : IQuery<LedgerPageResponse<LedgerDebtResponse>>;
