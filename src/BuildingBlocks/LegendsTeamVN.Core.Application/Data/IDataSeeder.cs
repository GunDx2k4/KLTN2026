namespace LegendsTeamVN.Core.Application.Data;

public interface IDataSeeder
{
    int Order => 0;
    Task SeedAsync(CancellationToken cancellationToken = default);
}

