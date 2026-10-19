using LegendsTeamVN.BadmintonClub.Domain.Entities;

namespace LegendsTeamVN.BadmintonClub.Domain.Repositories;

public interface IOutboxRepository
{
    void Add(OutboxMessage message);
    /// <summary>Claims only message kinds supported by the calling worker.</summary>
    Task<OutboxMessage?> ClaimAsync(IReadOnlyCollection<string> kinds, DateTime now, CancellationToken ct);
    Task CompleteAsync(Guid id, Guid leaseId, DateTime now, CancellationToken ct);
    Task FailAsync(Guid id, Guid leaseId, DateTime now, string error, CancellationToken ct);
}
