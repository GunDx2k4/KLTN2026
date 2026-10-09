using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using LegendsTeamVN.BadmintonClub.Persistence.Seeders;
using LegendsTeamVN.Core.Application.Data;
using LegendsTeamVN.Core.Persistence.DependencyInjection.Extensions;
using LegendsTeamVN.Core.Utilities.Options;
using Microsoft.Extensions.DependencyInjection;

namespace LegendsTeamVN.BadmintonClub.Persistence.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, ConnectionStringsOptions connectionStrings)
    {
        services.AddInterceptorPersistence();

        services.AddPostgreSQLDbContextUnitOfWork<BadmintonDbContext>(connectionStrings);

        services.AddScoped<IClubRepository, ClubRepository>();
        services.AddScoped<IClubMemberRepository, ClubMemberRepository>();

        return services;
    }

    public static IServiceCollection AddDataSeederBadminton(this IServiceCollection services)
    {
        services.AddTransient<IDataSeeder, ClubDataSeeder>();
        return services;
    }
}
