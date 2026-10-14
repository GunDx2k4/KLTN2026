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
        services.AddScoped<IGroupRoleRepository, GroupRoleRepository>();
        services.AddScoped<IJoinRequestRepository, JoinRequestRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IRecruitmentRepository, RecruitmentRepository>();
        services.AddScoped<IScheduleRepository, ScheduleRepository>();
        services.AddScoped<LegendsTeamVN.BadmintonClub.Application.Abstractions.IGroupAuthorizationService, LegendsTeamVN.BadmintonClub.Persistence.Services.GroupAuthorizationService>();

        return services;
    }

    public static IServiceCollection AddDataSeederBadminton(this IServiceCollection services)
    {
        services.AddTransient<GroupPermissionSeeder>();
        services.AddTransient<BadmintonUserSeeder>();
        services.AddTransient<BadmintonDemoDataSeeder>();
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddEnumerable(
            services,
            ServiceDescriptor.Transient<IDataSeeder, GroupPermissionSeeder>()
        );
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddEnumerable(
            services,
            ServiceDescriptor.Transient<IDataSeeder, BadmintonUserSeeder>()
        );
        Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddEnumerable(
            services,
            ServiceDescriptor.Transient<IDataSeeder, BadmintonDemoDataSeeder>()
        );
        return services;
    }
}
