using LegendsTeamVN.BadmintonClub.Presentation.Endpoints;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Users;

public sealed class UserEndpointRouteTests
{
    private static WebApplication CreateApplication()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Services.AddApiVersioning();
        builder.Services.AddSingleton(Substitute.For<ISender>());
        var app = builder.Build();
        new UserEndpoint().MapEndpoint(app);
        return app;
    }

    private static RouteEndpoint[] Routes(WebApplication app) => ((IEndpointRouteBuilder)app).DataSources
        .SelectMany(s => s.Endpoints).OfType<RouteEndpoint>().ToArray();

    [Fact]
    public async Task UserRoutes_KeepExistingPathsMethodsAndAuthorization()
    {
        await using var app = CreateApplication();
        var routes = Routes(app);
        var expected = new (string Name, string Method, string Suffix)[]
        {
            ("GetUserProfile", "GET", "profile"),
            ("UpdateUserProfile", "PUT", "profile"),
            ("GetMe", "GET", "me"),
            ("GetMyPermissions", "GET", "permissions"),
            ("GetAllPermissions", "GET", "all-permissions"),
            ("GetUsers", "GET", ""),
            ("GetUserById", "GET", "{id:guid}"),
            ("CreateUser", "POST", ""),
            ("UpdateUser", "PUT", "{id:guid}"),
            ("DeleteUser", "DELETE", "{id:guid}"),
            ("ToggleUserLock", "PUT", "{id:guid}/lock"),
            ("UpdateUserRoles", "PUT", "{id:guid}/roles"),
            ("ResetUserPassword", "POST", "{id:guid}/reset-password")
        };

        foreach (var (name, method, suffix) in expected)
        {
            var route = Assert.Single(routes, e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == name);
            Assert.Equal(("api/v{version:apiversion}/user/" + suffix).TrimEnd('/'), route.RoutePattern.RawText!.Trim('/'));
            Assert.Equal(method, Assert.Single(route.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods));
            Assert.NotEmpty(route.Metadata.GetOrderedMetadata<IAuthorizeData>());
            Assert.DoesNotContain(route.Metadata.GetOrderedMetadata<IAuthorizeData>(), a => a.Policy == "SessionOperations");
        }
        Assert.Equal(expected.Length, routes.Length);
    }
}
