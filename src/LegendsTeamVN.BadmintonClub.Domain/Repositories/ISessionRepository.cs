using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.Core.Domain.Repositories;

namespace LegendsTeamVN.BadmintonClub.Domain.Repositories;

public interface ISessionRepository : IGenericRepository<BadmintonSession, Guid>
{
    Task<IReadOnlyList<Guid>> FindPersonalScheduleRecipientsAsync(Guid sessionId, CancellationToken ct);
    Task<bool> CanAccessPersonalScheduleAsync(Guid sessionId, Guid userId, CancellationToken ct);
    Task<Services.MatchInvitationRoster?> GetMatchInvitationRosterAsync(Guid sessionId, Guid matchId, bool requirePlaying, CancellationToken ct);
    Task<PersonalRotationSnapshot> GetPersonalRotationAsync(Guid sessionId, Guid userId, bool forUpdate, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> FindDueRotationIdsAsync(DateTime now, int limit, IReadOnlyCollection<Guid> excluded, CancellationToken ct);
    Task<IReadOnlyList<Guid>> FindRotationRecoveryIdsAsync(DateTime now, Guid? after, int limit, CancellationToken ct);
    Task<BadmintonSession?> GetAttendanceAsync(Guid sessionId, bool forUpdate, CancellationToken cancellationToken = default);
    Task<BadmintonSession?> GetSettlementAsync(Guid sessionId, bool forUpdate, CancellationToken cancellationToken = default);
    void AddSettlementFees(IEnumerable<SessionFee> fees);
    Task<bool> CanManageFinanceAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> CanManageAttendanceAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> CanManageRotationAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default);
    Task<BadmintonSession?> GetRotationAsync(Guid sessionId, bool forUpdate, CancellationToken cancellationToken = default);
    void AddRotationMatches(IEnumerable<SessionMatch> matches);
    void SynchronizeRotationPlayers(SessionMatch match, IReadOnlyCollection<MatchPlayer> previousPlayers);
    Task<bool> CanEditManualRotationAsync(Guid groupId, Guid userId, CancellationToken ct = default);
    Task<bool> CanReceiveMatchInvitationAsync(Guid sessionId, Guid matchId, Guid userId, CancellationToken ct);
}
