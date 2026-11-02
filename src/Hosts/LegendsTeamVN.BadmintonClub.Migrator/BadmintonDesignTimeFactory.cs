using LegendsTeamVN.BadmintonClub.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LegendsTeamVN.BadmintonClub.Migrator;

public sealed class BadmintonDesignTimeFactory : IDesignTimeDbContextFactory<BadmintonDbContext>
{
    public BadmintonDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("BADMINTON_MIGRATION_CONNECTION")
            ?? throw new InvalidOperationException("Set BADMINTON_MIGRATION_CONNECTION explicitly before using EF tooling.");
        return new(new DbContextOptionsBuilder<BadmintonDbContext>().UseNpgsql(connection,
            x => x.MigrationsAssembly(typeof(BadmintonDesignTimeFactory).Assembly.FullName)).Options, new DesignPublisher());
    }

    private sealed class DesignPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
