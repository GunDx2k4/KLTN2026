using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.Core.Application.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using LegendsTeamVN.BadmintonClub.Application.Features.Attendance;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation;

namespace LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMessageHandler(Assembly.GetExecutingAssembly());
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<AttendanceService>();
        services.AddScoped<RotationService>();
        services.AddOptions<SessionRotationOptions>()
            .BindConfiguration(SessionRotationOptions.SectionName);

        return services;
    }
}
