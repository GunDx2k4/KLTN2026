using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Persistence.Repositories;

public sealed class MatchRepository : GenericRepository<BadmintonDbContext, Match, Guid>, IMatchRepository
{
    private readonly BadmintonDbContext context;

    public MatchRepository(BadmintonDbContext context) : base(context)
    {
        this.context = context;
    }

    public Task<Match?> FindDetailsAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Matches.AsNoTracking()
            .Include(x => x.BookingDetail)
            .Include(x => x.MatchPlayers.Where(p => !p.IsDeleted))
            .AsSingleQuery()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<Match?> FindForRsvpAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("An active transaction is required to lock a match for RSVP.");

        // PostgreSQL keeps the row lock until the caller commits/rolls back its transaction.
        var matches = await context.Matches.FromSqlInterpolated(
            $"SELECT * FROM \"Matches\" WHERE \"Id\" = {id} FOR UPDATE")
            .ToListAsync(cancellationToken);
        var match = matches.SingleOrDefault();
        if (match is null) return null;

        await context.Entry(match).Reference(x => x.BookingDetail).LoadAsync(cancellationToken);
        await context.Entry(match).Collection(x => x.MatchPlayers).Query()
            .IgnoreQueryFilters().LoadAsync(cancellationToken);
        context.Entry(match).Collection(x => x.MatchPlayers).IsLoaded = true;
        return match;
    }

    public void AddPlayer(MatchPlayer player) => context.MatchPlayers.Add(player);
}
