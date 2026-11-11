using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger.ListTransactions;

public sealed record ListLedgerTransactionsQuery(Guid ClubId, LedgerReadFilter Filter) : IQuery<LedgerPageResponse<LedgerTransactionResponse>>;
