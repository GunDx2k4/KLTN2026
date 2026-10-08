using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Models;
using LegendsTeamVN.Core.Domain.Repositories;

namespace LegendsTeamVN.BadmintonClub.Domain.Repositories;

public interface IMatchRepository : IGenericRepository<Match, Guid>
{
    Task<Match?> FindDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    // Caller must open a transaction before obtaining this lock.
    Task<Match?> FindForRsvpAsync(Guid id, CancellationToken cancellationToken = default);
    void AddPlayer(MatchPlayer player);
}

public interface IMatchRsvpLock
{
    Task<IAsyncDisposable?> TryAcquireAsync(Guid matchId, CancellationToken cancellationToken);
}

public interface IMatchAttendanceNotifier
{
    Task NotifyAsync(MatchAttendanceSnapshot attendance, Guid userId, CancellationToken cancellationToken);
}
