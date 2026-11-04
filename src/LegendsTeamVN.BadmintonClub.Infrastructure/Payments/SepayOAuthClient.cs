using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Payments;

public sealed class SepayOAuthClient(HttpClient http, TimeProvider clock) : ISepayOAuthClient
{
    private const string Origin = "https://my.sepay.vn";

    public Task<SepayTokens> ExchangeAsync(string clientId, string secret, string callbackUrl, string code, CancellationToken ct) =>
        TokenAsync(new() { ["grant_type"] = "authorization_code", ["client_id"] = clientId,
            ["client_secret"] = secret, ["redirect_uri"] = callbackUrl, ["code"] = code }, ct);

    public Task<SepayTokens> RefreshAsync(string clientId, string secret, string refreshToken, CancellationToken ct) =>
        TokenAsync(new() { ["grant_type"] = "refresh_token", ["client_id"] = clientId,
            ["client_secret"] = secret, ["refresh_token"] = refreshToken }, ct);

    private async Task<SepayTokens> TokenAsync(Dictionary<string, string> fields, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Origin + "/oauth/token") { Content = new FormUrlEncodedContent(fields) };
        var json = await SendAsync(request, ct);
        var access = json.GetProperty("access_token").GetString();
        var refresh = json.GetProperty("refresh_token").GetString();
        var expires = json.GetProperty("expires_in").GetInt32();
        if (string.IsNullOrWhiteSpace(access) || string.IsNullOrWhiteSpace(refresh) || expires is < 1 or > 31536000 ||
            !string.Equals(Text(json, "token_type"), "Bearer", StringComparison.OrdinalIgnoreCase))
            throw InvalidResponse();
        return new(access, refresh, clock.GetUtcNow().UtcDateTime.AddSeconds(expires));
    }

    public async Task<long> CompanyAsync(string token, CancellationToken ct)
    {
        var data = (await GetAsync("/api/v1/company", token, ct)).GetProperty("data");
        var id = data.GetProperty("id").GetInt64();
        if (id <= 0 || Text(data, "status") != "active") throw InvalidResponse();
        return id;
    }

    public async Task<IReadOnlyList<SepayRemoteBank>> BanksAsync(string token, CancellationToken ct)
    {
        var rows = await PagesAsync("/api/v1/bank-accounts", token, ct);
        return rows.Select(x => new SepayRemoteBank(x.GetProperty("id").GetInt64(), Text(x, "account_number"),
            Text(x, "account_holder_name"), Text(x.GetProperty("bank"), "bin"), Text(x.GetProperty("bank"), "short_name"),
            Active(x))).ToArray();
    }

    public async Task<IReadOnlyList<SepayRemoteWebhook>> WebhooksAsync(string token, string url, CancellationToken ct)
    {
        var rows = await PagesAsync("/api/v1/webhooks?webhook_url=" + Uri.EscapeDataString(url), token, ct);
        return rows.Where(x => Text(x, "webhook_url") == url).Select(x => new SepayRemoteWebhook(
            x.GetProperty("id").GetInt64(), Text(x, "webhook_url"), x.GetProperty("bank_account_id").GetInt64(),
            Text(x, "authen_type"), Text(x, "secret_key"), Active(x))).ToArray();
    }

    public async Task<long> CreateWebhookAsync(string token, long bankId, string url, string secret, CancellationToken ct)
    {
        using var request = Authorized(HttpMethod.Post, "/api/v1/webhooks", token);
        request.Content = JsonContent.Create(new { bank_mode = "single", bank_account_id = bankId, name = "BadmintonClub",
            event_type = "In_only", authen_type = "HMAC_SHA256", webhook_url = url, secret_key = secret,
            request_content_type = "Json", is_verify_payment = 1, skip_if_no_code = 0, active = 1,
            only_va = 0, va_mode = "none", retry_conditions = new[] { "non_2xx_status_code" } });
        var id = (await SendAsync(request, ct)).GetProperty("data").GetProperty("id").GetInt64();
        return id > 0 ? id : throw InvalidResponse();
    }

    public async Task DeleteWebhookAsync(string token, long id, CancellationToken ct)
    {
        using var request = Authorized(HttpMethod.Delete, "/api/v1/webhooks/" + id, token);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound) throw ProviderFailure(response.StatusCode);
    }

    private async Task<IReadOnlyList<JsonElement>> PagesAsync(string path, string token, CancellationToken ct)
    {
        var rows = new List<JsonElement>();
        for (var page = 1; page <= 100; page++)
        {
            var json = await GetAsync(path + (path.Contains('?') ? "&" : "?") + "limit=100&page=" + page, token, ct);
            var data = json.GetProperty("data");
            if (data.ValueKind != JsonValueKind.Array) throw InvalidResponse();
            rows.AddRange(data.EnumerateArray().Select(x => x.Clone()));
            if (!json.TryGetProperty("meta", out var meta))
            {
                if (data.GetArrayLength() == 0) return rows;
                throw InvalidResponse();
            }
            var pagination = meta.GetProperty("pagination");
            var current = pagination.GetProperty("current_page").GetInt32();
            if (current != page) throw InvalidResponse();
            if (current >= pagination.GetProperty("last_page").GetInt32()) return rows;
        }
        throw new SepaySetupException("TooManyAccounts", "Danh sách SePay vượt giới hạn hỗ trợ. Vui lòng liên hệ quản trị viên.");
    }

    private async Task<JsonElement> GetAsync(string path, string token, CancellationToken ct)
    {
        using var request = Authorized(HttpMethod.Get, path, token);
        return await SendAsync(request, ct);
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, Origin + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private async Task<JsonElement> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) throw ProviderFailure(response.StatusCode);
            if (response.Content.Headers.ContentLength is > 1048576) throw InvalidResponse();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions { MaxDepth = 32 }, ct);
            return document.RootElement.Clone();
        }
        catch (JsonException) { throw InvalidResponse(); }
        catch (HttpRequestException) { throw new SepaySetupException("ProviderUnavailable", "Không kết nối được SePay. Vui lòng thử lại.", 503); }
    }

    private static bool Active(JsonElement json) => json.TryGetProperty("active", out var value) &&
        (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n) && n == 1);
    private static string Text(JsonElement json, string name) => json.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
    private static SepaySetupException InvalidResponse() => new("ProviderResponse", "SePay trả dữ liệu không phù hợp. Chưa thể hoàn tất kết nối.", 503);
    private static SepaySetupException ProviderFailure(HttpStatusCode status) => new(
        status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? "AuthorizationRequired" : "ProviderUnavailable",
        status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            ? "SePay yêu cầu cấp lại quyền hoặc bổ sung quyền quản lý company, ngân hàng và webhook."
            : "SePay chưa xử lý được yêu cầu. Vui lòng thử lại.", 503);
}
