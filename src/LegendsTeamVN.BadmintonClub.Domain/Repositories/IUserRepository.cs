using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.Core.Domain.Repositories;

namespace LegendsTeamVN.BadmintonClub.Domain.Repositories;

public interface IUserRepository : IGenericRepository<AppUser, Guid>
{
    Task<bool> IsUserExistAsync(string? phone, string? email, CancellationToken cancellationToken = default);
    Task<AppUser?> FindByEmailOrPhoneAsync(string emailOrPhone, CancellationToken cancellationToken = default);
    Task<AppUser?> GetUserWithMembershipsAsync(Guid userId, CancellationToken cancellationToken = default);
}
