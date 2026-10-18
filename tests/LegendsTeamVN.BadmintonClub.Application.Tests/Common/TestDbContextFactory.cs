using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using LegendsTeamVN.BadmintonClub.Persistence;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Common;

public static class TestDbContextFactory
{
    public static BadmintonDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<BadmintonDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .Options;

        var publisher = Substitute.For<IPublisher>();
        return new BadmintonDbContext(options, publisher);
    }
}
