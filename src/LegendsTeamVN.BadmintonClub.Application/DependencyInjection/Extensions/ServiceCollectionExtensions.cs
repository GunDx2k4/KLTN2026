using LegendsTeamVN.Core.Application.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using LegendsTeamVN.BadmintonClub.Application.Features.Attendance;

namespace LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMessageHandler(Assembly.GetExecutingAssembly());
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<AttendanceService>();

        return services;
    }
}
