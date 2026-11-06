using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Services;
namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;
public interface IReputationRepository
{
    Task<ReputationAssessmentSource?> LockSessionAsync(Guid sessionId, CancellationToken ct);
    Task<IReadOnlyList<AppUser>> LockUsersAsync(IReadOnlyList<Guid> ids, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<ReputationScoreEntry>>> GetHistoriesAsync(IReadOnlyList<Guid> userIds, CancellationToken ct);
    Task<(IReadOnlyList<int> Ratings, IReadOnlyList<QualitySessionEvidence> Sessions)> GetQualitySourcesAsync(Guid groupId, CancellationToken ct);
    Task<GroupReview?> FindReviewAsync(Guid sessionId, Guid userId, CancellationToken ct);
    Task<BadmintonSession?> GetFinalizedAsync(Guid sessionId, CancellationToken ct);
    Task<bool> IsActiveUserAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<Guid>> GetActiveUserIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct);
    void Add(GroupReview review);
    void Add(Notification notification);
    void Add(OutboxMessage message);
}
