using LegendsTeamVN.BadmintonClub.Application.Features.Roles.Create;
using LegendsTeamVN.BadmintonClub.Application.Features.Roles.Delete;
using LegendsTeamVN.BadmintonClub.Application.Features.Roles.GetList;
using LegendsTeamVN.BadmintonClub.Application.Features.Roles.Update;
using LegendsTeamVN.BadmintonClub.Application.Features.Roles.UpdatePermissions;
using LegendsTeamVN.Core.Identity.Authorization;
using LegendsTeamVN.Core.Presentation.Abstractions;
using LegendsTeamVN.Core.Presentation.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LegendsTeamVN.BadmintonClub.Presentation.Endpoints;

public class RoleEndpoint : EndpointGroupBase
{
    protected override string Name => "roles";

    protected override void Map(RouteGroupBuilder group)
    {
        group.MapGet("", GetRoles)
             .WithName("GetRoles")
             .WithSummary("Get all roles")
             .WithDescription("Retrieves all roles in the system along with their assigned permissions.")
             .RequirePermission(AppPermissions.Roles.Read);

        group.MapPost("", CreateRole)
             .WithName("CreateRole")
             .WithSummary("Create new role")
             .WithDescription("Creates a new role definition.")
             .RequirePermission(AppPermissions.Roles.Create);

        group.MapPut("{id:guid}", UpdateRole)
             .WithName("UpdateRole")
             .WithSummary("Update role")
             .WithDescription("Updates the name and description of an existing role.")
             .RequirePermission(AppPermissions.Roles.Update);

        group.MapDelete("{id:guid}", DeleteRole)
             .WithName("DeleteRole")
             .WithSummary("Delete role")
             .WithDescription("Deletes a role from the system.")
             .RequirePermission(AppPermissions.Roles.Delete);

        group.MapPut("{id:guid}/permissions", UpdateRolePermissions)
             .WithName("UpdateRolePermissions")
             .WithSummary("Assign permissions to role")
             .WithDescription("Assigns a list of permissions to a specific role.")
             .RequirePermission(AppPermissions.Roles.AssignPermissions);
    }

    private static async Task<IResult> GetRoles(ISender sender)
    {
        var result = await sender.Send(new GetRolesQuery());
        return result.Match(onSuccess: response => Results.Ok(response));
    }

    private static async Task<IResult> CreateRole(CreateRoleCommand command, ISender sender)
    {
        var result = await sender.Send(command);
        return result.Match(onSuccess: () => Results.Ok());
    }

    private static async Task<IResult> UpdateRole(Guid id, UpdateRoleRequest request, ISender sender)
    {
        var result = await sender.Send(new UpdateRoleCommand(id, request.Name, request.Description));
        return result.Match(onSuccess: () => Results.NoContent());
    }

    private static async Task<IResult> DeleteRole(Guid id, ISender sender)
    {
        var result = await sender.Send(new DeleteRoleCommand(id));
        return result.Match(onSuccess: () => Results.NoContent());
    }

    private static async Task<IResult> UpdateRolePermissions(Guid id, UpdateRolePermissionsRequest request, ISender sender)
    {
        var result = await sender.Send(new UpdateRolePermissionsCommand(id, request.Permissions));
        return result.Match(onSuccess: () => Results.NoContent());
    }
}

public record UpdateRoleRequest(string Name, string? Description);
public record UpdateRolePermissionsRequest(List<string> Permissions);
