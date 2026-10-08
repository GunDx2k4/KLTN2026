using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Application.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace LegendsTeamVN.BadmintonClub.Persistence.Seeders;

internal sealed class MatchDataSeeder(BadmintonDbContext dbContext, IHostEnvironment environment) : IDataSeeder
{
    private const string SeedDescription = "UC06 RSVP test match (development seed)";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment() || await dbContext.Matches.AnyAsync(cancellationToken))
        {
            return;
        }

        var hostId = await dbContext.Database
            .SqlQuery<Guid>($"""
                SELECT "Id" AS "Value"
                FROM "Identity"."AppUsers"
                WHERE "UserName" = {"admin"}
                LIMIT 1
                """)
            .FirstOrDefaultAsync(cancellationToken);

        var court = await dbContext.Courts
            .OrderBy(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (hostId == Guid.Empty || court is null)
        {
            return;
        }

        var utcDate = DateTimeOffset.UtcNow.UtcDateTime.Date;
        var timeStart = new DateTimeOffset(utcDate, TimeSpan.Zero).AddDays(7).AddHours(11);
        var timeEnd = timeStart.AddHours(2);
        var bookingPrice = court.PricePerHour * 2;

        var booking = new Booking(hostId, bookingPrice);
        booking.UpdateStatus(BookingStatus.Confirmed);

        var bookingDetail = new BookingDetail(booking.Id, court.Id, timeStart, timeEnd, bookingPrice,
            BookingDetailStatus.Confirmed);
        var match = new Match(bookingDetail.Id, hostId, maxPlayers: 1, pricePerPlayer: 50000,
            registrationClosesAt: timeStart.AddHours(-1), SeedDescription);

        await dbContext.Bookings.AddAsync(booking, cancellationToken);
        await dbContext.BookingDetails.AddAsync(bookingDetail, cancellationToken);
        await dbContext.Matches.AddAsync(match, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
