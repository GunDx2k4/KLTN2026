using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.Core.Domain.Repositories;

namespace LegendsTeamVN.BadmintonClub.Domain.Repositories;

public interface INotificationRepository : IGenericRepository<Notification, Guid>
{
    Task<Guid?> ResolveCashRequestSessionIdAsync(Guid feeId, Guid recipientId, CancellationToken ct);
    Task<Guid?> ResolveLedgerGroupIdAsync(Guid ledgerId, Guid recipientId, CancellationToken ct);
}
