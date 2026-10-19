using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Persistence.Repositories;

namespace LegendsTeamVN.BadmintonClub.Persistence.Repositories;

public sealed class NotificationRepository(BadmintonDbContext dbContext)
    : GenericRepository<BadmintonDbContext, Notification, Guid>(dbContext), INotificationRepository
{
}
