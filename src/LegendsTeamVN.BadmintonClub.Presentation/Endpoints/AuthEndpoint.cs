using LegendsTeamVN.BadmintonClub.Application.Features.Auth.ExternalRegister;
using LegendsTeamVN.BadmintonClub.Application.Features.Auth.Register;
using LegendsTeamVN.BadmintonClub.Application.Features.Auth.Login;
using LegendsTeamVN.Core.Presentation.Abstractions;
using LegendsTeamVN.Core.Presentation.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using LegendsTeamVN.BadmintonClub.Application.Features.Auth.Logout;
using System.Security.Claims;
using LegendsTeamVN.BadmintonClub.Application.Features.Auth.Refresh;

namespace LegendsTeamVN.BadmintonClub.Presentation.Endpoints;

public class AuthEndpoint : EndpointGroupBase
{
    protected override string Name => "auth";

    protected override void Map(RouteGroupBuilder group)
    {
        group.MapPost("register", Register)
             .WithName("RegisterUser")
             .WithSummary("Register a new user")
             .WithDescription("Creates a new user account.");

        group.MapPost("external-register", ExternalRegister)
             .WithName("ExternalRegisterUser")
             .WithSummary("Register or login with external provider")
             .WithDescription("Registers or authenticates a user via external provider like Google or Apple.");

        group.MapPost("login", Login)
             .WithName("LoginUser")
             .WithSummary("Login to the system")
             .WithDescription("Returns a JWT access token upon successful login and sets a refresh token cookie.");
             
        group.MapPost("refresh", Refresh)
             .WithName("RefreshToken")
             .WithSummary("Refresh access token")
             .WithDescription("Uses the refresh token from cookies and access token from Authorization header to issue a new access token.");

        group.MapPost("logout", Logout)
             .WithName("LogoutUser")
             .WithSummary("Logout from the system")
             .WithDescription("Revokes the session and clears the refresh token cookie.")
             .RequireAuthorization();

    }

    private static async Task<IResult> Register(RegisterCommand command, ISender sender, HttpContext context)
    {
        var result = await sender.Send(command);

        return result.Match(
            onSuccess: response =>
            {
                if (!string.IsNullOrEmpty(response.RefreshToken))
                {
                    context.Response.Cookies.Append("refreshToken", response.RefreshToken, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        Expires = response.RefreshTokenExpiryTime
                    });
                }

                return Results.Created($"/api/v1/users/{response.UserId}", response);
            }
        );
    }

    private static async Task<IResult> ExternalRegister(ExternalRegisterCommand command, ISender sender, HttpContext context)
    {
        var result = await sender.Send(command);

        return result.Match(
            onSuccess: response =>
            {
                if (!string.IsNullOrEmpty(response.RefreshToken))
                {
                    context.Response.Cookies.Append("refreshToken", response.RefreshToken, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        Expires = response.RefreshTokenExpiryTime
                    });
                }

                return Results.Ok(response);
            }
        );
    }

    private static async Task<IResult> Login(LoginCommand command, ISender sender, HttpContext context)
    {
        var result = await sender.Send(command);

        return result.Match(
            onSuccess: response =>
            {
                if (!string.IsNullOrEmpty(response.RefreshToken))
                {
                    context.Response.Cookies.Append("refreshToken", response.RefreshToken, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        Expires = response.RefreshTokenExpiryTime
                    });
                }
                
                return Results.Ok(response);
            }
        );
    }
    
    private static async Task<IResult> Refresh(HttpContext context, ISender sender)
    {
        var refreshToken = context.Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Results.Unauthorized();
        }

        var authorizationHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
        {
            return Results.Unauthorized();
        }

        var accessToken = authorizationHeader.Substring("Bearer ".Length).Trim();
        var command = new RefreshTokenCommand(accessToken, refreshToken);
        var result = await sender.Send(command);

        return result.Match(
            onSuccess: response =>
            {
                if (!string.IsNullOrEmpty(response.RefreshToken))
                {
                    context.Response.Cookies.Append("refreshToken", response.RefreshToken, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        Expires = response.RefreshTokenExpiryTime
                    });
                }
                return Results.Ok(new { AccessToken = response.AccessToken });
            }
        );
    }

    private static async Task<IResult> Logout(LogoutRequest? request, HttpContext context, ISender sender)
    {
        string? accessToken = null;
        var authorizationHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authorizationHeader) && authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            accessToken = authorizationHeader.Substring("Bearer ".Length).Trim();
        }

        var command = new LogoutCommand(request?.DeviceToken, accessToken);
        var result = await sender.Send(command);

        context.Response.Cookies.Delete("refreshToken");
        return result.Match(
            onSuccess: () => Results.Ok(new { Message = "Đăng xuất thành công." })
        );
    }

}

public record LogoutRequest(string? DeviceToken = null);
