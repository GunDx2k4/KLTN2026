using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.Core.Domain.Repositories;

namespace LegendsTeamVN.BadmintonClub.Domain.Repositories;

public interface IClubRepository : IGenericRepository<ClubGroup, Guid>
{
    Task<List<GroupPermission>> GetAllPermissionsAsync(CancellationToken cancellationToken = default);
}
