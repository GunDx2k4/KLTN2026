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
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<LegendsTeamVN.BadmintonClub.Application.Abstractions.IGroupAuthorizationService, LegendsTeamVN.BadmintonClub.Persistence.Services.GroupAuthorizationService>();

        return services;
    }

    public static IServiceCollection AddDataSeederBadminton(this IServiceCollection services)
    {
        services.AddTransient<GroupPermissionSeeder>();
        services.AddTransient<BadmintonUserSeeder>();
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddEnumerable(
            services,
            ServiceDescriptor.Transient<IDataSeeder, GroupPermissionSeeder>()
        );
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddEnumerable(
            services,
            ServiceDescriptor.Transient<IDataSeeder, BadmintonUserSeeder>()
        );
        return services;
    }
}
