using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.Core.Application.Data;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Persistence.Seeders;

internal sealed class CourtDataSeeder(BadmintonDbContext dbContext) : IDataSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await dbContext.Set<Court>().AnyAsync(cancellationToken))
        {
            return;
        }

        // Ensure SportType exists
        var sportType = await dbContext.Set<SportType>().FirstOrDefaultAsync(cancellationToken);
        if (sportType == null)
        {
            sportType = new SportType("Cầu lông");
            await dbContext.Set<SportType>().AddAsync(sportType, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // Ensure Venue exists
        var venue = await dbContext.Set<Venue>().FirstOrDefaultAsync(cancellationToken);
        if (venue == null)
        {
            venue = new Venue(
                "Sân cầu lông AZ",
                "123 Nguyễn Trãi, Thanh Xuân, Hà Nội",
                20.9947m,
                105.8072m,
                new TimeOnly(8, 0),
                new TimeOnly(22, 0),
                "Sân cầu lông 5 sao, không gian rộng rãi, có điều hòa."
            );
            await dbContext.Set<Venue>().AddAsync(venue, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var courts = new List<Court>
        {
            new Court("Sân số 1 (VIP)", "Sân thảm chất lượng cao", 150000, venue.Id, sportType.Id),
            new Court("Sân số 2", "Sân thường", 100000, venue.Id, sportType.Id),
            new Court("Sân số 3", "Sân thường", 100000, venue.Id, sportType.Id)
        };

        await dbContext.Set<Court>().AddRangeAsync(courts, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
