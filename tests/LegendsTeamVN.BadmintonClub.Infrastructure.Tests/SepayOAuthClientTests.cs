using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Infrastructure.Payments;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Tests;

public sealed class SepayOAuthClientTests
{
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request);
    }
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };

    [Fact]
    public async Task TokenExchangeUsesFixedProviderAndFormBodyAndDoesNotExposeSecretInRecords()
    {
        using var http = new HttpClient(new Handler(async request =>
        {
            Assert.Equal("https://my.sepay.vn/oauth/token", request.RequestUri!.ToString());
            Assert.Equal("application/x-www-form-urlencoded", request.Content!.Headers.ContentType!.MediaType);
            var body = await request.Content.ReadAsStringAsync();
            Assert.Contains("grant_type=authorization_code", body);
            Assert.Contains("client_secret=private-secret", body);
            return Json(new { access_token = "private-access", refresh_token = "private-refresh", expires_in = 3600, token_type = "Bearer" });
        }));
        var result = await new SepayOAuthClient(http, TimeProvider.System).ExchangeAsync("client", "private-secret", "https://api.example.test/callback", "private-code", default);
        Assert.Equal("private-access", result.AccessToken);
        Assert.DoesNotContain("private-access", result.ToString());
        Assert.DoesNotContain("private-secret", new SepayWebhookCredential("123", "MBBank", "private-secret").ToString());
    }

    [Fact]
    public async Task RefreshUsesRotatingRefreshGrant()
    {
        using var http = new HttpClient(new Handler(async request =>
        {
            var body = await request.Content!.ReadAsStringAsync();
            Assert.Contains("grant_type=refresh_token", body);
            Assert.Contains("refresh_token=old-refresh", body);
            Assert.DoesNotContain("redirect_uri=", body);
            return Json(new { access_token = "new-access", refresh_token = "new-refresh", expires_in = 3600, token_type = "Bearer" });
        }));
        var result = await new SepayOAuthClient(http, TimeProvider.System).RefreshAsync("client", "secret", "old-refresh", default);
        Assert.Equal("new-refresh", result.RefreshToken);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"access_token\":\"a\",\"refresh_token\":\"b\",\"expires_in\":\"3600\",\"token_type\":\"Bearer\"}")]
    [InlineData("{\"access_token\":\"a\",\"refresh_token\":\"b\",\"expires_in\":3600,\"token_type\":\"other\"}")]
    [InlineData("{\"status\":false}")]
    public async Task MalformedProviderResponsesAreRejectedWithoutEchoingPayload(string body)
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") })));
        var error = await Assert.ThrowsAsync<SepaySetupException>(() => new SepayOAuthClient(http, TimeProvider.System).ExchangeAsync("client", "private-secret", "https://api.example.test/callback", "code", default));
        Assert.Equal("ProviderResponse", error.Code);
        Assert.DoesNotContain("private-secret", error.Message);
    }

    [Fact]
    public async Task BankPaginationFollowsProviderMetadataAndPreservesLeadingZeros()
    {
        var calls = 0;
        using var http = new HttpClient(new Handler(request =>
        {
            calls++;
            Assert.Equal("my.sepay.vn", request.RequestUri!.Host);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Contains("page=" + calls, request.RequestUri.Query);
            return Task.FromResult(Json(new { status = "success", data = new[] { new { id = calls, account_number = "00123", account_holder_name = "CLB",
                active = true, bank = new { bin = "970422", short_name = "MBBank" } } },
                meta = new { pagination = new { current_page = calls, last_page = 2 } } }));
        }));
        var accounts = await new SepayOAuthClient(http, TimeProvider.System).BanksAsync("token", default);
        Assert.Equal(2, accounts.Count);
        Assert.All(accounts, account => Assert.Equal("00123", account.AccountNumber));
    }

    [Fact]
    public async Task CreateWebhookUsesHmacExactBankAndNoCodeFilterForManualReview()
    {
        using var http = new HttpClient(new Handler(async request =>
        {
            Assert.Equal("https://my.sepay.vn/api/v1/webhooks", request.RequestUri!.ToString());
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal(19, body.RootElement.GetProperty("bank_account_id").GetInt64());
            Assert.Equal("single", body.RootElement.GetProperty("bank_mode").GetString());
            Assert.Equal("HMAC_SHA256", body.RootElement.GetProperty("authen_type").GetString());
            Assert.Equal("private-hmac", body.RootElement.GetProperty("secret_key").GetString());
            Assert.Equal(0, body.RootElement.GetProperty("skip_if_no_code").GetInt32());
            Assert.Equal("none", body.RootElement.GetProperty("va_mode").GetString());
            Assert.Equal("non_2xx_status_code", body.RootElement.GetProperty("retry_conditions")[0].GetString());
            return Json(new { status = "success", data = new { id = 23 } });
        }));
        Assert.Equal(23, await new SepayOAuthClient(http, TimeProvider.System).CreateWebhookAsync("token", 19, "https://api.example.test/webhook", "private-hmac", default));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.Found)]
    public async Task ProviderErrorsCannotBecomeSuccessfulConnectionOrEchoProviderBody(HttpStatusCode status)
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("private-token") })));
        var error = await Assert.ThrowsAsync<SepaySetupException>(() => new SepayOAuthClient(http, TimeProvider.System).CompanyAsync("private-token", default));
        Assert.DoesNotContain("private-token", error.ToString());
        Assert.Equal(503, error.Status);
    }
}
