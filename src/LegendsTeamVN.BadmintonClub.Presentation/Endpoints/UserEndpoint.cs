using LegendsTeamVN.BadmintonClub.Application.DTOs.Users.Requests;
using LegendsTeamVN.BadmintonClub.Application.Features.Users.Create;
using LegendsTeamVN.BadmintonClub.Application.Features.Users.Delete;
using LegendsTeamVN.BadmintonClub.Application.Features.Users.GetAllPermissions;
using LegendsTeamVN.BadmintonClub.Application.Features.Users.GetById;
using LegendsTeamVN.BadmintonClub.Application.Features.Users.GetList;
using LegendsTeamVN.BadmintonClub.Application.Features.Users.GetPermissions;
using LegendsTeamVN.BadmintonClub.Application.Features.Users.Me;
using LegendsTeamVN.BadmintonClub.Application.Features.Users.ResetPassword;
using LegendsTeamVN.BadmintonClub.Application.Features.Users.ToggleLock;
using LegendsTeamVN.BadmintonClub.Application.Features.Users.Update;
using LegendsTeamVN.BadmintonClub.Application.Features.Users.UpdateRoles;
using LegendsTeamVN.Core.Identity.Authorization;
using LegendsTeamVN.Core.Presentation.Abstractions;
using LegendsTeamVN.Core.Presentation.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LegendsTeamVN.BadmintonClub.Presentation.Endpoints;

public class UserEndpoint : EndpointGroupBase
{
    protected override string Name => "user";

    protected override void Map(RouteGroupBuilder group)
    {
        group.MapGet("me", GetMe)
             .WithName("GetMe")
             .WithSummary("Get current user details")
             .WithDescription("Returns details, roles, and tree permissions of the currently authenticated user.")
             .RequireAuthorization();

        group.MapGet("permissions", GetMyPermissions)
             .WithName("GetMyPermissions")
             .WithSummary("Get current user permissions")
             .WithDescription("Returns roles and grouped tree permissions of the currently authenticated user.")
             .RequireAuthorization();

        group.MapGet("all-permissions", GetAllPermissions)
             .WithName("GetAllPermissions")
             .WithSummary("Get all system permissions")
             .WithDescription("Returns all system permissions grouped in a multi-level tree hierarchy.")
             .RequirePermission(AppPermissions.Users.Read);

        group.MapGet("", GetUsers)
             .WithName("GetUsers")
             .WithSummary("Gets all users with pagination")
             .WithDescription("Retrieves a paged list of all users based on filter criteria.")
             .RequirePermission(AppPermissions.Users.Read);

        group.MapGet("{id:guid}", GetUserById)
             .WithName("GetUserById")
             .WithSummary("Get user details by ID")
             .WithDescription("Retrieves details, roles, and permissions of a specific user by ID.")
             .RequirePermission(AppPermissions.Users.Read);

        group.MapPost("", CreateUser)
             .WithName("CreateUser")
             .WithSummary("Create user")
             .WithDescription("Creates a new user account.")
             .RequirePermission(AppPermissions.Users.Create);

        group.MapPut("{id:guid}", UpdateUser)
             .WithName("UpdateUser")
             .WithSummary("Update user")
             .WithDescription("Updates user email, username, or phone number.")
             .RequirePermission(AppPermissions.Users.Update);

        group.MapDelete("{id:guid}", DeleteUser)
             .WithName("DeleteUser")
             .WithSummary("Delete user")
             .WithDescription("Deletes a user account.")
             .RequirePermission(AppPermissions.Users.Delete);

        group.MapPut("{id:guid}/lock", ToggleUserLock)
             .WithName("ToggleUserLock")
             .WithSummary("Lock or unlock user")
             .WithDescription("Locks or unlocks a user account.")
             .RequirePermission(AppPermissions.Users.Lock);

        group.MapPut("{id:guid}/roles", UpdateUserRoles)
             .WithName("UpdateUserRoles")
             .WithSummary("Assign roles to user")
             .WithDescription("Assigns roles to a user account.")
             .RequirePermission(AppPermissions.Users.AssignRoles);

        group.MapPost("{id:guid}/reset-password", ResetUserPassword)
             .WithName("ResetUserPassword")
             .WithSummary("Reset user password")
             .WithDescription("Resets password for a user account.")
             .RequirePermission(AppPermissions.Users.ResetPassword);
    }

    private static async Task<IResult> GetMe(ISender sender)
    {
        var result = await sender.Send(new MeQuery());
        return result.Match(onSuccess: response => Results.Ok(response));
    }

    private static async Task<IResult> GetMyPermissions(ISender sender)
    {
        var result = await sender.Send(new GetMyPermissionsQuery());
        return result.Match(onSuccess: response => Results.Ok(response));
    }

    private static async Task<IResult> GetAllPermissions(ISender sender)
    {
        var result = await sender.Send(new GetAllPermissionsQuery());
        return result.Match(onSuccess: response => Results.Ok(response));
    }

    private static async Task<IResult> GetUsers([AsParameters] GetUsersRequest request, ISender sender)
    {
        var result = await sender.Send(new GetUsersQuery(request));
        return result.Match(onSuccess: response => Results.Ok(response));
    }

    private static async Task<IResult> GetUserById(Guid id, ISender sender)
    {
        var result = await sender.Send(new GetUserByIdQuery(id));
        return result.Match(onSuccess: response => Results.Ok(response));
    }

    private static async Task<IResult> CreateUser(CreateUserCommand command, ISender sender)
    {
        var result = await sender.Send(command);
        return result.Match(onSuccess: userId => Results.Created($"/api/v1/user/{userId}", userId));
    }

    private static async Task<IResult> UpdateUser(Guid id, UpdateUserRequest request, ISender sender)
    {
        var result = await sender.Send(new UpdateUserCommand(id, request.Email, request.UserName, request.PhoneNumber));
        return result.Match(onSuccess: () => Results.NoContent());
    }

    private static async Task<IResult> DeleteUser(Guid id, ISender sender)
    {
        var result = await sender.Send(new DeleteUserCommand(id));
        return result.Match(onSuccess: () => Results.NoContent());
    }

    private static async Task<IResult> ToggleUserLock(Guid id, ToggleUserLockRequest request, ISender sender)
    {
        var result = await sender.Send(new ToggleUserLockCommand(id, request.IsLocked));
        return result.Match(onSuccess: () => Results.NoContent());
    }

    private static async Task<IResult> UpdateUserRoles(Guid id, UpdateUserRolesRequest request, ISender sender)
    {
        var result = await sender.Send(new UpdateUserRolesCommand(id, request.Roles));
        return result.Match(onSuccess: () => Results.NoContent());
    }

    private static async Task<IResult> ResetUserPassword(Guid id, ResetUserPasswordRequest request, ISender sender)
    {
        var result = await sender.Send(new ResetUserPasswordCommand(id, request.NewPassword));
        return result.Match(onSuccess: () => Results.NoContent());
    }
}

public record UpdateUserRequest(string Email, string? UserName, string? PhoneNumber);
public record ToggleUserLockRequest(bool IsLocked);
public record ResetUserPasswordRequest(string NewPassword);
