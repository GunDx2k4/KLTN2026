using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.Features.Ledger;
using LegendsTeamVN.BadmintonClub.Infrastructure.DependencyInjection.Extensions;
using LegendsTeamVN.BadmintonClub.Presentation.Endpoints;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Identity.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Ledger;

public sealed class LedgerWriteHttpTests
{
    [LedgerIntegrationFact]
    public async Task JwtMultipartAndMonthlyRoutesUseRealTransactionsAndRejectBoundaryViolations()
    {
        await using var f = await LedgerWriteTests.Fixture.Create();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        foreach (var descriptor in f.Registrations) builder.Services.Add(descriptor);
        LegendsTeamVN.Core.Presentation.DependencyInjection.Extensions.ServiceCollectionExtensions.AddCorePresentation(builder.Services);
        LegendsTeamVN.Core.Presentation.DependencyInjection.Extensions.SwaggerExtensions.AddCoreSwagger(builder.Services);
        LegendsTeamVN.BadmintonClub.Presentation.DependencyInjection.Extensions.ServiceCollectionExtensions.AddPresentation(builder.Services);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
        builder.Services.AddSessionOperationsAuthentication(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Jwt:Issuer"] = "uc19-write-tests", ["Jwt:Audience"] = "uc19-write-tests", ["Jwt:SecretKey"] = new string('w', 64) }).Build());
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
        var expenseOperation = swagger.Paths.Values.SelectMany(p => p.Operations.Values).Single(o => o.OperationId == "CreateLedgerExpense");
        var schema = expenseOperation.RequestBody.Content["multipart/form-data"].Schema;
        Assert.Equal("binary", schema.Properties["invoice"].Format);
        Assert.Contains("requestId", schema.Required);
        Assert.Contains("amount", schema.Required);
        Assert.Contains("description", schema.Required);
        var downloadOperation = swagger.Paths.Values.SelectMany(p => p.Operations.Values).Single(o => o.OperationId == "GetLedgerAttachment");
        Assert.Equal("binary", downloadOperation.Responses["200"].Content["application/pdf"].Schema.Format);
        Assert.Equal(8, swagger.Paths.Values.SelectMany(p => p.Operations.Values).Count(o => new[] { "CreateLedgerExpense", "IssueMonthlyContribution", "RecordMonthlyContribution", "GetLedgerOverview", "ListLedgerTransactions", "ListLedgerDebts", "ListMonthlyContributionCandidates", "GetLedgerAttachment" }.Contains(o.OperationId)));
        using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
        var path = $"/api/v1/clubs/{f.Group}/ledger";
        var jwt = new JwtSecurityToken("uc19-write-tests", "uc19-write-tests", [new Claim("sub", f.Actor.ToString())],
            expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('w', 64))), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
        var issuedResponse = await client.PostAsJsonAsync(path + "/monthly-contributions", new { userId = f.Payer, contributionMonth = "2026-11-01" });
        Assert.Equal(HttpStatusCode.OK, issuedResponse.StatusCode);
        var issued = (await issuedResponse.Content.ReadFromJsonAsync<LedgerWriteResponse>())!;
        var requestId = Guid.NewGuid();
        var collectPath = path + $"/monthly-contributions/{issued.MonthlyContributionId}/collect";
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(collectPath, new { requestId, expectedAmount = 300000m })).StatusCode);
        var replay = (await (await client.PostAsJsonAsync(collectPath, new { requestId, expectedAmount = 300000m })).Content.ReadFromJsonAsync<LedgerWriteResponse>())!;
        Assert.True(replay.Replayed);
        using var form = Expense(100m, "%PDF-1.7 invoice"u8.ToArray());
        var expense = await client.PostAsync(path + "/transactions", form);
        Assert.Equal(HttpStatusCode.OK, expense.StatusCode);
        var written = (await expense.Content.ReadFromJsonAsync<LedgerWriteResponse>())!;
        var downloaded = await client.GetAsync(path + $"/transactions/{written.LedgerId}/attachment");
        Assert.Equal(HttpStatusCode.OK, downloaded.StatusCode);
        Assert.Equal("application/pdf", downloaded.Content.Headers.ContentType!.MediaType);
        Assert.Equal("no-store", downloaded.Headers.CacheControl!.ToString());
        using var oversized = Expense(1, new byte[5242881]);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.PostAsync(path + "/transactions", oversized)).StatusCode);
        using var fake = Expense(1, "HTML"u8.ToArray());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(path + "/transactions", fake)).StatusCode);
        client.DefaultRequestHeaders.Add("X-Group-Id", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(collectPath, new { requestId, expectedAmount = 300000m })).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Group-Id");
        f.Flags.WritesEnabled = false;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync(collectPath, new { requestId, expectedAmount = 300000m })).StatusCode);
        await app.StopAsync();
    }

    private static MultipartFormDataContent Expense(decimal amount, byte[] content)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(Guid.NewGuid().ToString()), "requestId");
        form.Add(new StringContent(amount.ToString(System.Globalization.CultureInfo.InvariantCulture)), "amount");
        form.Add(new StringContent("OTHER"), "category");
        form.Add(new StringContent("Chi nước uống"), "description");
        var file = new ByteArrayContent(content); file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "invoice", "receipt.pdf");
        return form;
    }
}
