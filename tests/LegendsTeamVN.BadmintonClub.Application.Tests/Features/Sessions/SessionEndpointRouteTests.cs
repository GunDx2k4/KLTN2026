using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Presentation.Endpoints;
using LegendsTeamVN.BadmintonClub.Presentation.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Sessions;

public sealed class SessionEndpointRouteTests
{
    private static WebApplication CreateApplication()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Services.AddApiVersioning();
        builder.Services.AddSingleton(Substitute.For<ISender>());
        var app = builder.Build();
        new UserEndpoint().MapEndpoint(app);
        new SessionEndpoint().MapEndpoint(app);
        return app;
    }

    private static RouteEndpoint[] Routes(WebApplication app) => ((IEndpointRouteBuilder)app).DataSources
        .SelectMany(s => s.Endpoints).OfType<RouteEndpoint>().ToArray();

    [Theory]
    [InlineData("GetMyMatches", "GET", "{sessionId:guid}/my-matches", false)]
    [InlineData("PauseMyMatches", "POST", "{sessionId:guid}/my-matches/pause", false)]
    [InlineData("ResumeMyMatches", "POST", "{sessionId:guid}/my-matches/resume", false)]
    [InlineData("ConfirmManualMatch", "POST", "{sessionId:guid}/matches/{matchId:guid}/confirm-manual-start", true)]
    [InlineData("SetRotationPause", "PUT", "{sessionId:guid}/rotation/players/{userId:guid}/pause", true)]
    [InlineData("RestoreManualLineup", "POST", "{sessionId:guid}/matches/{matchId:guid}/restore-proposal", true)]
    [InlineData("UpdateManualLineup", "PUT", "{sessionId:guid}/matches/{matchId:guid}/manual-lineup", true)]
    public async Task RotationRoutes_PreservePathsMethodsAndDistinctParticipantAndHostAuthorization(
        string name, string method, string suffix, bool hostOnly)
    {
        await using var app = CreateApplication();
        var route = Assert.Single(Routes(app), e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == name);

        Assert.Equal("api/v{version:apiversion}/sessions/" + suffix, route.RoutePattern.RawText!.Trim('/'));
        Assert.Equal(method, Assert.Single(route.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods));
        Assert.Equal("SessionOperations", Assert.Single(route.Metadata.GetOrderedMetadata<IAuthorizeData>()).Policy);
        var groupRequirements = route.Metadata.GetOrderedMetadata<AuthorizationPolicy>()
            .SelectMany(p => p.Requirements).OfType<GroupPermissionRequirement>().ToArray();
        if (hostOnly)
            Assert.Equal(GroupPermissions.Operations.MatchManage, Assert.Single(groupRequirements).Permission);
        else
            Assert.Empty(groupRequirements);
        Assert.Contains(route.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>(), p =>
            p.StatusCode == 200 && p.Type == (hostOnly ? typeof(RotationResponse) : typeof(MyMatchesResponse)));
        Assert.Contains(hostOnly ? "FR16 - Can thiệp đội hình thủ công" : "FR17 - Lịch thi đấu cá nhân",
            route.Metadata.GetMetadata<ITagsMetadata>()!.Tags);
    }

    [Fact]
    public async Task UserAndSessionRoutes_HaveNoDuplicateNamesOrMethodPaths()
    {
        await using var app = CreateApplication();
        var routes = Routes(app);

        Assert.All(routes.GroupBy(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()!.EndpointName),
            group => Assert.Single(group));
        var paths = routes.SelectMany(e => e.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods
            .Select(method => method + " " + e.RoutePattern.RawText!.Trim('/')));
        Assert.All(paths.GroupBy(p => p, StringComparer.OrdinalIgnoreCase), group => Assert.Single(group));
    }
}
