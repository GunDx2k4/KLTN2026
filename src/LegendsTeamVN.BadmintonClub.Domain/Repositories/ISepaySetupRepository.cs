using LegendsTeamVN.BadmintonClub.Domain.Entities;

namespace LegendsTeamVN.BadmintonClub.Domain.Repositories;

public interface ISepaySetupRepository
{
    Task<SepayApplicationSettings?> GetApplicationAsync(CancellationToken ct);
    Task<bool> CanManageApplicationAsync(Guid userId, CancellationToken ct);
    Task<ClubSepayConnection?> GetConnectionAsync(Guid bankAccountId, CancellationToken ct);
    Task<ClubSepayConnection?> GetConnectionByIdAsync(Guid connectionId, CancellationToken ct);
    Task<ClubSepayConnection?> GetWebhookConnectionAsync(Guid bankAccountId, CancellationToken ct);
    Task<IReadOnlyList<ClubSepayConnection>> ListConnectionsAsync(Guid groupId, CancellationToken ct);
    Task<bool> HasConnectionsAsync(CancellationToken ct);
    Task<bool> IsProviderBankInUseAsync(long companyId, long providerBankId, Guid connectionId, CancellationToken ct);
    void AddApplication(SepayApplicationSettings settings);
    void AddConnection(ClubSepayConnection connection);
    Task SaveAsync(CancellationToken ct);
}
