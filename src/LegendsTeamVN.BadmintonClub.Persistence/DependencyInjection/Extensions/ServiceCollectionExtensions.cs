using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using LegendsTeamVN.Core.Persistence.DependencyInjection.Extensions;
using LegendsTeamVN.Core.Utilities.Options;
using LegendsTeamVN.Core.Application.Data;
using LegendsTeamVN.BadmintonClub.Persistence.Seeders;

using Microsoft.Extensions.DependencyInjection;

namespace LegendsTeamVN.BadmintonClub.Persistence.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, ConnectionStringsOptions connectionStrings)
    {
        services.AddInterceptorPersistence();

        services.AddPostgreSQLDbContextUnitOfWork<BadmintonDbContext>(connectionStrings);

        services.AddScoped<ICourtRepository, CourtRepository>();
        services.AddScoped<IVenueScheduleRepository, VenueScheduleRepository>();
        services.AddScoped<ICourtPricingRepository, CourtPricingRepository>();
        services.AddScoped<IVenueRepository, VenueRepository>();

        return services;
    }

    public static IServiceCollection AddDataSeederBadminton(this IServiceCollection services)
    {
        services.AddTransient<IDataSeeder, VenueDataSeeder>();
        services.AddTransient<IDataSeeder, CourtDataSeeder>();
        return services;
    }
}
