using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Requests;
using LegendsTeamVN.BadmintonClub.Infrastructure.DependencyInjection.Extensions;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Extensions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;
using LegendsTeamVN.BadmintonClub.Application.Tests.Common;
using LegendsTeamVN.BadmintonClub.Presentation.Endpoints;
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

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Ledger;

public sealed class LedgerHttpTests
{
    [Fact]
    public async Task RealRoutesRejectCrossGroupInvalidPaginationAndUnprivilegedAdmin()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        LegendsTeamVN.Core.Presentation.DependencyInjection.Extensions.ServiceCollectionExtensions.AddCorePresentation(builder.Services);
        LegendsTeamVN.Core.Presentation.DependencyInjection.Extensions.SwaggerExtensions.AddCoreSwagger(builder.Services);
        LegendsTeamVN.BadmintonClub.Presentation.DependencyInjection.Extensions.ServiceCollectionExtensions.AddPresentation(builder.Services);
        builder.Services.AddSessionOperationsAuthentication(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Jwt:Issuer"] = "uc19-tests", ["Jwt:Audience"] = "uc19-tests", ["Jwt:SecretKey"] = new string('k', 64) }).Build());
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddApplication();
        var flags = new LedgerOptions { Enabled = true };
        builder.Services.AddSingleton<IOptionsMonitor<LedgerOptions>>(new TestOptionsMonitor<LedgerOptions>(flags));
        var current = Substitute.For<ICurrentUserService>();
        var actor = Guid.NewGuid();
        current.UserId.Returns(actor);
        builder.Services.AddSingleton(current);
        var repo = Substitute.For<ILedgerReadRepository>();
        var group = Guid.NewGuid();
        repo.HasPermissionAsync(group, actor, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        repo.GetOverviewAsync(group, Arg.Any<LedgerReadFilter>(), Arg.Any<CancellationToken>())
            .Returns(new LedgerOverviewResponse(0, 0, 0, 0, 0, 0, DateTime.UtcNow, new(true, true, false)));
        builder.Services.AddSingleton(repo);
        var guard = Substitute.For<IGroupAuthorizationService>();
        guard.HasPermissionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        builder.Services.AddSingleton(guard);
        await using var app = builder.Build();
        app.UseExceptionHandler();
        new LedgerEndpoint().MapEndpoint(app);
        app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        var swagger = app.Services.GetRequiredService<Swashbuckle.AspNetCore.Swagger.ISwaggerProvider>().GetSwagger("v1");
        var operations = swagger.Paths.Values.SelectMany(p => p.Operations.Values).ToArray();
        var expense = Assert.Single(operations, o => o.OperationId == "CreateLedgerExpense");
        var form = expense.RequestBody.Content["multipart/form-data"].Schema;
        Assert.Equal("binary", form.Properties["invoice"].Format);
        Assert.Contains("requestId", form.Required);
        Assert.Contains("amount", form.Required);
        Assert.Contains("description", form.Required);
        var download = Assert.Single(operations, o => o.OperationId == "GetLedgerAttachment");
        foreach (var contentType in new[] { "application/pdf", "image/png", "image/jpeg" })
            Assert.Equal("binary", download.Responses["200"].Content[contentType].Schema.Format);

        using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
        var path = $"/api/v1/clubs/{group}/ledger";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        var jwt = new JwtSecurityToken("uc19-tests", "uc19-tests", [new Claim("sub", actor.ToString()), new Claim(ClaimTypes.Role, "Admin")],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('k', 64))), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path + "/transactions?page=0")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Group-Id", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Group-Id");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/clubs/{Guid.NewGuid()}/ledger")).StatusCode);
        flags.Enabled = false;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await client.PostAsync(path + "/transactions", null)).StatusCode);
        await app.StopAsync();
    }
}
