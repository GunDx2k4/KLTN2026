using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Domain.Repositories;

public interface IDeviceRepository
{
    Task<UserDevice?> FindByTokenAsync(string deviceToken, CancellationToken cancellationToken = default);
    Task UpsertDeviceAsync(Guid userId, string deviceToken, DevicePlatform? platform, CancellationToken cancellationToken = default);
    Task DeactivateDeviceAsync(Guid userId, string deviceToken, CancellationToken cancellationToken = default);
}
