using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using LegendsTeamVN.BadmintonClub.Persistence;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Common;

public static class TestDbContextFactory
{
    // Handler tests exercise state changes with InMemory. PostgreSQL row locks and the
    // UnitOfWork transaction must be verified separately against a real PostgreSQL database.
    public static ISessionRepository CreateAttendanceSessionRepository(BadmintonDbContext dbContext)
    {
        var repository = Substitute.For<ISessionRepository>();
        repository.GetAttendanceAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var sessionId = call.ArgAt<Guid>(0);
                var ct = call.ArgAt<CancellationToken>(2);
                var session = await dbContext.BadmintonSessions
                    .SingleOrDefaultAsync(s => s.Id == sessionId && !s.IsDeleted, ct);
                if (session is null) return null;
                await dbContext.Entry(session).Collection(s => s.Bookings).LoadAsync(ct);
                await dbContext.Entry(session).Collection(s => s.Recruitments).LoadAsync(ct);
                return session;
            });
        return repository;
    }

    public static BadmintonDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<BadmintonDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .Options;

        var publisher = Substitute.For<IPublisher>();
        return new BadmintonDbContext(options, publisher);
    }
}
