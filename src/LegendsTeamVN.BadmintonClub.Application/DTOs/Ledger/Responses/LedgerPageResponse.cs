using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;

public sealed record LedgerPageResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
