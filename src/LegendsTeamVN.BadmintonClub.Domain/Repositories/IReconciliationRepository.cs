using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Domain.Repositories;

public interface IReconciliationRepository
{
    Task<GroupBankAccount?> GetBankAsync(Guid bankAccountId, CancellationToken ct);
    Task<BankTransaction?> FindProviderAsync(long providerTransactionId, CancellationToken ct);
    Task<IReadOnlyList<BankTransaction>> FindReferenceAsync(string referenceCode, CancellationToken ct);
    Task<bool> CanManageFinanceAsync(Guid groupId, Guid userId, CancellationToken ct);
    Task<(SessionFee Fee, BadmintonSession Session, BankTransaction? Transaction)?> LoadForConfirmationAsync(
        Guid groupId, Guid feeId, Guid? transactionId, string? referenceCode, CancellationToken ct);
    Task LockProviderAsync(long providerTransactionId, CancellationToken ct);
    Task LockReferenceAsync(string? referenceCode, CancellationToken ct);
    Task<BankTransaction?> GetAsync(Guid groupId, Guid transactionId, CancellationToken ct);
    Task<(IReadOnlyList<BankTransaction> Transactions, int Total)> ListAsync(Guid groupId,
        ReconciliationStatus? status, int page, int pageSize, CancellationToken ct);
    Task<Guid?> FindFeeByPaymentCodeAsync(Guid groupId, string code, CancellationToken ct);
    Task<IReadOnlyList<Guid>> ManagersAsync(Guid groupId, CancellationToken ct);
    Task<bool> CanReceiveConfirmationAsync(Guid feeId, Guid groupId, Guid sessionId, Guid userId, CancellationToken ct);
    Task<bool> CanReceiveAlertAsync(Guid transactionId, Guid groupId, Guid userId, CancellationToken ct);
    void AddTransaction(BankTransaction transaction);
    void AddLedger(GroupLedger ledger);
}
