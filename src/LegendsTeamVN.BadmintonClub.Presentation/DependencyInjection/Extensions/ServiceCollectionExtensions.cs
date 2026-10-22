using LegendsTeamVN.BadmintonClub.Presentation.Security;
using LegendsTeamVN.BadmintonClub.Presentation.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using LegendsTeamVN.Core.Presentation.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace LegendsTeamVN.BadmintonClub.Presentation.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        // The core catch-all is registered first by the host; specific failures must run before it.
        services.Insert(0, ServiceDescriptor.Singleton<IExceptionHandler, LineupSnapshotExceptionHandler>());
        services.AddScoped<IAuthorizationHandler, GroupPermissionHandler>();
        services.AddEndpoints(Assembly.GetExecutingAssembly());
        return services;
    }
}
