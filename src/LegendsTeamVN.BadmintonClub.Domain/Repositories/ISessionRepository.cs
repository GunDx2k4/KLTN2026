using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.Core.Domain.Repositories;

namespace LegendsTeamVN.BadmintonClub.Domain.Repositories;

public interface ISessionRepository : IGenericRepository<BadmintonSession, Guid>
{
    Task<IReadOnlyList<Guid>> FindDueRotationIdsAsync(DateTime now, int limit, IReadOnlyCollection<Guid> excluded, CancellationToken ct);
    Task<IReadOnlyList<Guid>> FindRotationRecoveryIdsAsync(DateTime now, Guid? after, int limit, CancellationToken ct);
    Task<BadmintonSession?> GetAttendanceAsync(Guid sessionId, bool forUpdate, CancellationToken cancellationToken = default);
    Task<bool> CanManageAttendanceAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> CanManageRotationAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default);
    Task<BadmintonSession?> GetRotationAsync(Guid sessionId, bool forUpdate, CancellationToken cancellationToken = default);
    void AddRotationMatches(IEnumerable<SessionMatch> matches);
    Task<bool> CanReceiveMatchInvitationAsync(Guid sessionId, Guid matchId, Guid userId, CancellationToken ct);
}
