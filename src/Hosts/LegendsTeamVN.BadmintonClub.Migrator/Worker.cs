using LegendsTeamVN.BadmintonClub.Persistence;
using LegendsTeamVN.Core.Identity.Data;

using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Migrator;

public class Worker(IServiceProvider serviceProvider, ILogger<Worker> logger, IHostApplicationLifetime hostApplicationLifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Migrator starting database migrations...");

        try
        {
            using var scope = serviceProvider.CreateScope();
            
            var identityContext = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
            await identityContext.Database.MigrateAsync(stoppingToken);
            logger.LogInformation("CoreIdentityDbContext migrated successfully.");

            var badmintonContext = scope.ServiceProvider.GetRequiredService<BadmintonDbContext>();
            await badmintonContext.Database.MigrateAsync(stoppingToken);
            logger.LogInformation("BadmintonDbContext migrated successfully.");

            logger.LogInformation("Running data seeders...");
            var seeders = scope.ServiceProvider.GetServices<LegendsTeamVN.Core.Application.Data.IDataSeeder>()
                .OrderBy(s => s is IdentityDataSeeder ? 0 : 1);
            foreach (var seeder in seeders)
            {
                logger.LogInformation("Running seeder: {SeederName}", seeder.GetType().Name);
                await seeder.SeedAsync(stoppingToken);
            }
            logger.LogInformation("Data seeding completed successfully.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "An error occurred while migrating databases.");
            throw;
        }

        logger.LogInformation("All database migrations completed successfully.");
        
        hostApplicationLifetime.StopApplication();
    }
}
