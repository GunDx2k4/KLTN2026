using LegendsTeamVN.BadmintonClub.Presentation.Security;
using LegendsTeamVN.Core.Presentation.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace LegendsTeamVN.BadmintonClub.Presentation.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, GroupPermissionHandler>();
        services.AddEndpoints(Assembly.GetExecutingAssembly());
        return services;
    }
}
