using LegendsTeamVN.BadmintonClub.Domain.Entities;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;

public sealed record ReconciliationResponse(Guid TransactionId, long? ProviderTransactionId, Guid? BankAccountId,
    Guid? FeeId, string? ReferenceCode, decimal AmountReceived, string TransferContent, DateTime TransactionTime,
    string Status, string? ReasonCode, Guid? ReconciledBy, DateTime? ReconciledAt, string? Note)
{
    public static ReconciliationResponse From(BankTransaction transaction) => new(transaction.Id, transaction.ProviderTransactionId,
        transaction.BankAccountId, transaction.FeeId, transaction.ReferenceCode, transaction.AmountReceived,
        transaction.TransferContent, transaction.TransactionTime, transaction.ReconciliationStatus.ToString(),
        transaction.ReasonCode, transaction.ReconciledBy, transaction.ReconciledAt, transaction.ReconciliationNote);
}

public sealed record ReconciliationListResponse(IReadOnlyList<ReconciliationResponse> Items, int Total, int Page, int PageSize);
