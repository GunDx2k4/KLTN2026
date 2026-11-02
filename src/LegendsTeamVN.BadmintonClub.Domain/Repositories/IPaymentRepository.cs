using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Domain.Repositories;

public interface IPaymentRepository
{
    Task<SessionFee?> GetMineAsync(Guid feeId, Guid userId, bool forUpdate, CancellationToken ct);
    Task<(IReadOnlyList<SessionFee> Fees, int Total)> ListMineAsync(Guid userId, int page, int pageSize, Guid? groupId, Guid? sessionId, PaymentStatus? status, CancellationToken ct);
    Task<bool> CanManageBankAsync(Guid groupId, Guid userId, CancellationToken ct);
    Task<IReadOnlyList<GroupBankAccount>> GetActiveBanksAsync(Guid groupId, bool forUpdate, CancellationToken ct);
    void AddBank(GroupBankAccount bank);
    Task<IReadOnlyList<Guid>> CashRecipientsAsync(Guid sessionId, CancellationToken ct);
    Task<bool> IsCashRequestPendingAsync(Guid feeId, Guid requestId, Guid recipientId, CancellationToken ct);
}
