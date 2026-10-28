using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.Core.Application.Data;
using Microsoft.Extensions.Logging;

namespace LegendsTeamVN.BadmintonClub.Persistence.Seeders;

public sealed class AttendanceDemoDataSeeder(
    BadmintonDbContext dbContext,
    ILogger<AttendanceDemoDataSeeder> logger) : IDataSeeder
{
    public int Order => 5;

    public Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // Consolidated into BadmintonDemoDataSeeder for unified realistic monthly timeline
        return Task.CompletedTask;
    }
}
