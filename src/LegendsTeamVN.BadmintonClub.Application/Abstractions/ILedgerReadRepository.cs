using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;

namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;

public interface ILedgerReadRepository
{
    Task<bool> HasLedgerAsync(Guid clubId, Guid ledgerId, CancellationToken ct);
    Task<bool> HasPermissionAsync(Guid clubId, Guid userId, string permission, CancellationToken ct);
    Task<LedgerOverviewResponse> GetOverviewAsync(Guid clubId, LedgerReadFilter filter, CancellationToken ct);
    Task<LedgerPageResponse<LedgerTransactionResponse>> ListTransactionsAsync(Guid clubId, Guid userId, LedgerReadFilter filter, CancellationToken ct);
    Task<LedgerPageResponse<LedgerDebtResponse>> ListDebtsAsync(Guid clubId, Guid userId, LedgerReadFilter filter, CancellationToken ct);
    Task<LedgerPageResponse<MonthlyContributionCandidateResponse>> ListMonthlyCandidatesAsync(Guid clubId, LedgerReadFilter filter, CancellationToken ct);
    Task<LedgerAttachmentResponse?> GetAttachmentAsync(Guid clubId, Guid ledgerId, CancellationToken ct);
}
