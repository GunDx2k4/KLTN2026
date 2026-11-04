using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Infrastructure.Payments;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using LegendsTeamVN.Core.Identity.Data;
using LegendsTeamVN.Core.Identity.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Payments;

public sealed partial class PaymentReconciliationHttpTests
{
    private sealed partial class Fixture
    {
        private AppIdentityDbContext OpenIdentity() => new(new DbContextOptionsBuilder<AppIdentityDbContext>()
            .UseNpgsql(Database.ConnectionString, o => o.MigrationsAssembly("LegendsTeamVN.BadmintonClub.Migrator")).Options);

        private async Task SeedSetupIdentity()
        {
            await using var identity = OpenIdentity();
            await identity.Database.MigrateAsync();
            var role = new LegendsTeamVN.Core.Identity.Entities.AppRole { Id = Guid.NewGuid(), Name = "Setup", NormalizedName = "SETUP", Description = "Test" };
            var permission = new LegendsTeamVN.Core.Identity.Entities.AppPermission { Name = AppPermissions.Integrations.SepayManage,
                GroupName = "Integrations", DisplayName = "SePay" };
            identity.AddRange(role, permission, new LegendsTeamVN.Core.Identity.Entities.AppUser { Id = Manager, UserName = "setup", NormalizedUserName = "SETUP" },
                new Microsoft.AspNetCore.Identity.IdentityUserRole<Guid> { UserId = Manager, RoleId = role.Id },
                new LegendsTeamVN.Core.Identity.Entities.AppRolePermission { RoleId = role.Id, PermissionId = permission.Id });
            await identity.SaveChangesAsync();
        }

        private void AddSetupServices(IServiceCollection services)
        {
            services.AddScoped(_ => OpenIdentity());
            services.AddScoped<ISepaySetupRepository, SepaySetupRepository>();
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddSingleton<ISepayOAuthClient>(Provider);
            services.AddScoped<SepaySetupService>();
            services.AddScoped<ISepaySetupService>(sp => sp.GetRequiredService<SepaySetupService>());
            services.AddScoped<ISepayWebhookCredentials>(sp => sp.GetRequiredService<SepaySetupService>());
        }

        public string SetupPath => "/api/v1/clubs/" + Group + "/sepay";
        public async Task ConfigureSetup()
        {
            var response = await Client.PutAsJsonAsync("/api/v1/sepay/application", new ConfigureSepayApplicationRequest(
                "client-test", "private-client-secret", "https://api.example.test", "https://app.example.test", true, null));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("private-client-secret", await response.Content.ReadAsStringAsync());
        }
        public async Task<string> StartSetup()
        {
            var response = await Client.PostAsync(SetupPath + "/authorize", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var dto = await response.Content.ReadFromJsonAsync<SepayAuthorizationResponse>();
            return QueryHelpers.ParseQuery(new Uri(dto!.AuthorizationUrl).Query)["state"].ToString();
        }
        public Task<HttpResponseMessage> Callback(string state, string code = "test-code") =>
            Client.GetAsync("/api/v1/sepay/oauth/callback?state=" + state + "&code=" + code);
        public async Task AuthorizeSetup()
        {
            await ConfigureSetup();
            var callback = await Callback(await StartSetup());
            Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
            Assert.StartsWith("https://app.example.test/#/club/" + Group + "/sepay?", callback.Headers.Location!.ToString());
        }
    }

    private sealed class FakeSepayOAuth : ISepayOAuthClient
    {
        public long Company { get; set; } = 123;
        public string Account { get; set; } = "00123456789";
        public string Secret { get; private set; } = "";
        public string WebhookUrl { get; private set; } = "";
        public int Creates { get; private set; }
        public int Exchanges { get; private set; }
        public bool FailAfterCreate { get; set; }
        public bool FailDelete { get; set; }
        private bool exists;
        public Task<SepayTokens> ExchangeAsync(string clientId, string secret, string callbackUrl, string code, CancellationToken ct)
        {
            Exchanges++;
            Assert.Equal("private-client-secret", secret);
            return Task.FromResult(new SepayTokens("private-access-token", "private-refresh-token", DateTime.UtcNow.AddHours(1)));
        }
        public Task<SepayTokens> RefreshAsync(string clientId, string secret, string refreshToken, CancellationToken ct) =>
            Task.FromResult(new SepayTokens("private-refreshed-access", "private-refreshed-refresh", DateTime.UtcNow.AddHours(1)));
        public Task<long> CompanyAsync(string accessToken, CancellationToken ct) => Task.FromResult(Company);
        public Task<IReadOnlyList<SepayRemoteBank>> BanksAsync(string accessToken, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SepayRemoteBank>>([new(19, Account, "CLB", "970436", "Vietcombank", true)]);
        public Task<IReadOnlyList<SepayRemoteWebhook>> WebhooksAsync(string accessToken, string url, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SepayRemoteWebhook>>(exists && WebhookUrl == url ? [new(23, url, 19, "HMAC_SHA256", Secret, true)] : []);
        public Task<long> CreateWebhookAsync(string accessToken, long bankId, string url, string secret, CancellationToken ct)
        {
            Creates++; Secret = secret; WebhookUrl = url; exists = true;
            if (FailAfterCreate) throw new SepaySetupException("ProviderTimeout", "Provider timeout", 503);
            return Task.FromResult(23L);
        }
        public Task DeleteWebhookAsync(string accessToken, long webhookId, CancellationToken ct)
        {
            if (FailDelete) throw new SepaySetupException("ProviderUnavailable", "Provider failure", 503);
            exists = false;
            return Task.CompletedTask;
        }
    }

    [ReconciliationIntegrationFact]
    public async Task SepaySetupConnectsBankEncryptsSecretsAndProcessesCompanyScopedWebhook()
    {
        await using var f = await Fixture.Create(setup: true);
        await f.AuthorizeSetup();
        var accounts = await f.Client.GetFromJsonAsync<SepayBankAccountResponse[]>(f.SetupPath + "/accounts");
        Assert.True(Assert.Single(accounts!).MatchesClubBank);
        Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsJsonAsync(f.SetupPath + "/account", new { providerBankId = 19 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsync(f.SetupPath + "/activate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsync(f.SetupPath + "/activate", null)).StatusCode);
        Assert.Equal(1, f.Provider.Creates);
        var status = await f.Client.GetStringAsync(f.SetupPath);
        foreach (var secret in new[] { "private-access-token", "private-refresh-token", f.Provider.Secret, "private-client-secret" }) Assert.DoesNotContain(secret, status);
        await using var db = f.Database.Open();
        var connection = await db.Set<ClubSepayConnection>().SingleAsync();
        Assert.DoesNotContain("private-access-token", connection.ProtectedTokens);
        Assert.NotEqual(f.Provider.Secret, connection.ProtectedWebhookSecret);
        Assert.Equal("CONNECTED", connection.Status);
        var body = f.Payload();
        var stamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signed = Encoding.UTF8.GetBytes(stamp.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".").Concat(body).ToArray();
        var signature = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(f.Provider.Secret), signed)).ToLowerInvariant();
        Assert.Equal(HttpStatusCode.OK, (await f.Send(body, signature, stamp)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Send(body, signature, stamp)).StatusCode);
        Assert.Equal("123", (await db.BankTransactions.SingleAsync()).ProviderCompanyId);
        Assert.Single(await db.GroupLedgers.ToListAsync());
        Assert.Equal(HttpStatusCode.OK, (await f.Client.DeleteAsync(f.SetupPath)).StatusCode);
        Assert.Equal(1, await db.GroupLedgers.CountAsync());
        Assert.NotEqual(HttpStatusCode.OK, (await f.Send(body, signature, stamp)).StatusCode);
    }

    [ReconciliationIntegrationFact]
    public async Task SepaySetupStateIsSingleUseExpiredStateAndCrossClubCannotConnect()
    {
        await using var f = await Fixture.Create(setup: true);
        await f.ConfigureSetup();
        var state = await f.StartSetup();
        Assert.Equal(HttpStatusCode.Redirect, (await f.Callback(state)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Callback(state)).StatusCode);
        Assert.Equal(1, f.Provider.Exchanges);
        var expired = await f.StartSetup();
        await f.Redis.GetDatabase().KeyDeleteAsync("sepay:oauth:state:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(expired))));
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Callback(expired)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.PostAsync("/api/v1/clubs/" + Guid.NewGuid() + "/sepay/authorize", null)).StatusCode);
        f.Login(f.User);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.GetAsync("/api/v1/sepay/application")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.PostAsync(f.SetupPath + "/authorize", null)).StatusCode);
    }

    [ReconciliationIntegrationFact]
    public async Task SepaySetupRejectsMismatchedBankAndRecoversWebhookCreationAfterProviderTimeout()
    {
        await using var f = await Fixture.Create(setup: true);
        await f.AuthorizeSetup();
        f.Provider.Account = "999999999";
        Assert.Equal(HttpStatusCode.Conflict, (await f.Client.PostAsJsonAsync(f.SetupPath + "/account", new { providerBankId = 19 })).StatusCode);
        Assert.Equal(0, f.Provider.Creates);
        f.Provider.Account = "00123456789";
        Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsJsonAsync(f.SetupPath + "/account", new { providerBankId = 19 })).StatusCode);
        f.Provider.FailAfterCreate = true;
        Assert.NotEqual(HttpStatusCode.OK, (await f.Client.PostAsync(f.SetupPath + "/activate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsync(f.SetupPath + "/activate", null)).StatusCode);
        Assert.Equal(1, f.Provider.Creates);
        f.Provider.FailDelete = true;
        Assert.NotEqual(HttpStatusCode.OK, (await f.Client.DeleteAsync(f.SetupPath)).StatusCode);
        Assert.Contains("CONNECTED", await f.Client.GetStringAsync(f.SetupPath));
        f.Provider.FailDelete = false;
        Assert.Equal(HttpStatusCode.OK, (await f.Client.DeleteAsync(f.SetupPath)).StatusCode);
    }

    [ReconciliationIntegrationFact]
    public async Task SepaySetupProviderIdsAreUniqueWithinCompanyAndLegacyIdsRemainProtected()
    {
        await using var f = await Fixture.Create();
        await using var db = f.Database.Open();
        var evidence = new LegendsTeamVN.BadmintonClub.Domain.Services.PaymentReconciliationEvidence(999, "Vietcombank", "00123456789", "", "in", 50000,
            DateTime.UtcNow, null, "missing code");
        var a = BankTransaction.Receive(f.Bank, evidence, "123");
        var b = BankTransaction.Receive(f.Bank, evidence, "456");
        db.AddRange(a, b);
        await db.SaveChangesAsync();
        var repository = new ReconciliationRepository(db);
        Assert.Equal(a.Id, (await repository.FindProviderAsync(999, "123", default))!.Id);
        Assert.Equal(b.Id, (await repository.FindProviderAsync(999, "456", default))!.Id);
        Assert.Null(await repository.FindProviderAsync(999, "789", default));
        Assert.Equal(0, await db.GroupLedgers.CountAsync());
    }
}
