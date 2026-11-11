using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;
public interface IReputationReadRepository
{
    Task<bool> CanDeliverAsync(string kind, Guid sessionId, Guid bookingId, Guid userId, DateTime? expiresAt, DateTime now, CancellationToken ct);
    Task<IReadOnlyList<Guid>> FindDueAsync(DateTime cutover, DateTime now, int batch, IReadOnlyList<Guid> excluded, CancellationToken ct);
    Task<PlayerReputationResponse?> GetPlayerAsync(Guid userId, CancellationToken ct);
    Task<GroupQualityResponse?> GetQualityAsync(Guid groupId, CancellationToken ct);
    Task<ReputationPageResponse<ReputationEntryResponse>> GetEntriesAsync(Guid userId, int page, int size, CancellationToken ct);
    Task<ReputationPageResponse<ReviewInvitationResponse>> GetInvitationsAsync(Guid userId, DateTime now, int page, int size, CancellationToken ct);
    Task<bool> CanAuditAsync(Guid groupId, Guid userId, CancellationToken ct);
    Task<ReputationAuditResponse?> GetAuditAsync(Guid groupId, Guid sessionId, int page, int size, CancellationToken ct);
}
