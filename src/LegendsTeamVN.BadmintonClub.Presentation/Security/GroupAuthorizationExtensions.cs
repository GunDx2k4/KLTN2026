using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;

namespace LegendsTeamVN.BadmintonClub.Presentation.Security;

public static class GroupAuthorizationExtensions
{
    public static TBuilder RequireGroupPermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.RequireAuthorization(policy => policy.AddRequirements(new GroupPermissionRequirement(permission)));
    }
}
