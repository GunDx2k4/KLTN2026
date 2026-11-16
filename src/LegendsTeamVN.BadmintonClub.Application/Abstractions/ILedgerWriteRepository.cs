using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Services;

namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;

public interface ILedgerWriteRepository
{
    Task<bool> LockGroupAsync(Guid groupId, CancellationToken ct);
    Task<GroupLedger?> FindRequestAsync(Guid groupId, Guid requestId, CancellationToken ct);
    Task<LedgerAttachment?> FindAttachmentAsync(Guid ledgerId, CancellationToken ct);
    Task<LedgerBalance> GetBalanceAsync(Guid groupId, CancellationToken ct);
    Task<bool> SessionBelongsToGroupAsync(Guid groupId, Guid sessionId, CancellationToken ct);
    Task<MonthlyContribution?> FindMonthAsync(Guid groupId, Guid userId, DateOnly month, CancellationToken ct);
    Task<MonthlyContribution?> LockContributionAsync(Guid groupId, Guid contributionId, CancellationToken ct);
    Task<GroupMember?> LockMemberAsync(Guid groupId, Guid userId, CancellationToken ct);
    Task<GroupFeeRule?> LockRuleAsync(Guid groupId, CancellationToken ct);
    Task<short?> GetGenderAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<Guid>> GetViewerIdsAsync(Guid groupId, CancellationToken ct);
    void Add(GroupLedger ledger);
    void Add(MonthlyContribution contribution);
    void Add(LedgerAttachment attachment);
    void Add(Notification notification);
    void Add(OutboxMessage message);
}
