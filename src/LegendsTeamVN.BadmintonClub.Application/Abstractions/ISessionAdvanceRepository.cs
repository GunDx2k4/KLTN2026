using LegendsTeamVN.BadmintonClub.Domain.Entities;

namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;

public interface ISessionAdvanceRepository
{
    Task<(GroupFeeRule? Rule, GroupMember? Member, short Gender)> GetPolicyAsync(Guid group, Guid user, CancellationToken ct);
    Task<BadmintonSession?> LoadSessionAsync(Guid id, bool locked, CancellationToken ct);
    Task<IReadOnlyList<SessionAdvancePayment>> ListMineAsync(Guid user, CancellationToken ct);
    Task<IReadOnlyList<SessionAdvancePayment>> ListClubAsync(Guid group, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, string>> GetPayerNamesAsync(Guid[] ids, CancellationToken ct);
    Task<Guid?> FindSessionAsync(Guid group, Guid advanceId, CancellationToken ct);
    Task<Guid?> FindSessionByCodeAsync(Guid group, string code, CancellationToken ct);
    Task<bool> HasRefundReferenceAsync(Guid group, string reference, Guid except, CancellationToken ct);
    Task<BankTransaction?> GetReviewTransactionAsync(Guid id, Guid bankId, CancellationToken ct);
    Task<BankTransaction?> LockTransactionAsync(Guid id, CancellationToken ct);
    void Add(SessionAdvancePayment advance);
    void Add(GroupLedger ledger);
}
