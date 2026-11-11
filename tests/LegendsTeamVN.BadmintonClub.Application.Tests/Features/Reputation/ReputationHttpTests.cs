using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Extensions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
using LegendsTeamVN.BadmintonClub.Application.Tests.Common;
using LegendsTeamVN.BadmintonClub.Infrastructure.DependencyInjection.Extensions;
using LegendsTeamVN.BadmintonClub.Presentation.Endpoints;
using LegendsTeamVN.Core.Domain.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Xunit;
namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Reputation;
public sealed class ReputationHttpTests
{
    [Fact]
    public async Task RealRoutesEnforceJwtPaginationAndAuditScope()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        LegendsTeamVN.Core.Presentation.DependencyInjection.Extensions.ServiceCollectionExtensions.AddCorePresentation(builder.Services);
        LegendsTeamVN.BadmintonClub.Presentation.DependencyInjection.Extensions.ServiceCollectionExtensions.AddPresentation(builder.Services);
        builder.Services.AddSessionOperationsAuthentication(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Jwt:Issuer"] = "uc20-tests", ["Jwt:Audience"] = "uc20-tests", ["Jwt:SecretKey"] = new string('k', 64) }).Build());
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddApplication();
        var settings = new ReputationOptions { EffectiveFromUtc = new DateTimeOffset(ReputationEngineTests.Now.AddDays(-30)) };
        builder.Services.AddSingleton<IOptionsMonitor<ReputationOptions>>(new TestOptionsMonitor<ReputationOptions>(settings));
        var actor = Guid.NewGuid();
        var current = Substitute.For<ICurrentUserService>(); current.UserId.Returns(actor);
        builder.Services.AddSingleton(current);
        var read = Substitute.For<IReputationReadRepository>();
        read.GetPlayerAsync(actor, Arg.Any<CancellationToken>()).Returns(new PlayerReputationResponse(actor, 79, "Rủi ro bùng kèo", "UC20-V1", ReputationEngineTests.Now, 3));
        read.GetEntriesAsync(actor, 1, 20, Arg.Any<CancellationToken>()).Returns(new ReputationPageResponse<ReputationEntryResponse>([], 1, 20, 0));
        builder.Services.AddSingleton(read);
        var writes = Substitute.For<IReputationRepository>();
        writes.IsActiveUserAsync(actor, Arg.Any<CancellationToken>()).Returns(true);
        builder.Services.AddSingleton(writes);
        builder.Services.AddSingleton(Substitute.For<IUnitOfWork>());
        var guard = Substitute.For<IGroupAuthorizationService>();
        guard.HasPermissionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        builder.Services.AddSingleton(guard);
        await using var app = builder.Build();
        app.UseExceptionHandler();
        new ReputationEndpoint().MapEndpoint(app); new UserEndpoint().MapEndpoint(app); new SessionEndpoint().MapEndpoint(app);
        app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
        var path = $"/api/v1/reputation/players/{actor}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        var jwt = new JwtSecurityToken("uc20-tests", "uc20-tests", [new Claim("sub", actor.ToString()), new Claim(ClaimTypes.Role, "Admin")],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('k', 64))), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("email", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("comment", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/reputation/me/entries?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/reputation/me/entries")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/user/me/reviews", new { SessionId = Guid.NewGuid(), Rating = 6 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/clubs/{Guid.NewGuid()}/sessions/{Guid.NewGuid()}/reputation-audit")).StatusCode);
        await app.StopAsync();
    }
}
