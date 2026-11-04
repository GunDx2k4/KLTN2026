using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Payments;

public sealed class SepaySetupService(ISepaySetupRepository repository, IPaymentRepository payments, IReconciliationRepository reconciliation,
    ISepayOAuthClient provider, IDataProtectionProvider protection, IConnectionMultiplexer redis,
    IOptionsMonitor<ReconciliationOptions> operations, TimeProvider clock) : ISepaySetupService, ISepayWebhookCredentials
{
    private readonly IDataProtector secrets = protection.CreateProtector("SePay.Secrets.v1");
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);
    private const string Scopes = "bank-account:read webhook:read webhook:write webhook:delete company";
    private sealed record AuthorizationState(Guid GroupId, Guid Actor, Guid BankAccountId, Guid Revision);

    public async Task<SepayApplicationResponse> GetApplicationAsync(Guid actor, CancellationToken ct)
    {
        await Admin(actor, ct);
        return Application(await repository.GetApplicationAsync(ct));
    }

    public Task<SepayApplicationResponse> ConfigureApplicationAsync(Guid actor, ConfigureSepayApplicationRequest input, CancellationToken ct) =>
        Locked("application", async token =>
        {
            await Admin(actor, token);
            var settings = await repository.GetApplicationAsync(token);
            if (settings?.Revision != input.Revision) throw Failure("Busy", "Cấu hình đã thay đổi. Vui lòng tải lại.");
            if (settings is not null && (settings.ClientId != input.ClientId.Trim() || settings.PublicApiBaseUrl != input.PublicApiBaseUrl.TrimEnd('/')) &&
                await repository.HasConnectionsAsync(token))
                throw Failure("ActiveConnections", "Cần ngắt các kết nối hiện có trước khi đổi Client ID hoặc địa chỉ backend.");
            var encrypted = string.IsNullOrEmpty(input.ClientSecret) ? settings?.ProtectedClientSecret : Protect(input.ClientSecret);
            if (string.IsNullOrEmpty(encrypted)) throw Failure("ClientSecretRequired", "Cần nhập Client Secret của ứng dụng.", 400);
            if (settings is null) { settings = SepayApplicationSettings.Create(); repository.AddApplication(settings); }
            settings.Configure(input.ClientId.Trim(), encrypted, input.PublicApiBaseUrl, input.FrontendBaseUrl,
                input.Enabled, actor, Now);
            await Admin(actor, token);
            await repository.SaveAsync(token);
            return Application(settings);
        }, ct);

    public async Task<SepayConnectionResponse> GetConnectionAsync(Guid groupId, Guid actor, CancellationToken ct)
    {
        await Manager(groupId, actor, ct);
        var banks = await payments.GetActiveBanksAsync(groupId, false, ct);
        var bank = banks.Count == 1 ? banks[0] : null;
        return Connection(bank, bank is null ? null : await repository.GetConnectionAsync(bank.Id, ct),
            await repository.GetApplicationAsync(ct));
    }

    public Task<SepayAuthorizationResponse> StartAsync(Guid groupId, Guid actor, CancellationToken ct) =>
        Locked("club:" + groupId, async token =>
        {
            var bank = await Bank(groupId, actor, token);
            var app = await ReadyApplication(token);
            var existing = await repository.GetConnectionAsync(bank.Id, token);
            if (existing?.Status is "CONNECTED" or "PROVISIONING" or "DISCONNECTING") throw Failure("AlreadyConnected", "Hãy hoàn tất ngắt kết nối hiện tại trước khi cấp quyền lại.");
            var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var value = JsonSerializer.Serialize(new AuthorizationState(groupId, actor, bank.Id, app.Revision));
            await redis.GetDatabase().StringSetAsync(StateKey(state), value, StateLifetime, When.NotExists).WaitAsync(token);
            var url = "https://my.sepay.vn/oauth/authorize?response_type=code&client_id=" + Uri.EscapeDataString(app.ClientId) +
                "&redirect_uri=" + Uri.EscapeDataString(app.CallbackUrl) + "&scope=" + Uri.EscapeDataString(Scopes) + "&state=" + state;
            return new SepayAuthorizationResponse(url);
        }, ct);

    public async Task<IReadOnlyList<SepayConnectionResponse>> ListConnectionsAsync(Guid groupId, Guid actor, CancellationToken ct)
    {
        await Manager(groupId, actor, ct);
        var app = await repository.GetApplicationAsync(ct);
        return (await repository.ListConnectionsAsync(groupId, ct)).Select(x => new SepayConnectionResponse(x.Id, x.BankAccountId,
            x.Status, x.CompanyId, x.Gateway, x.ConnectedAt, app?.Enabled == true,
            operations.CurrentValue.Enabled && operations.CurrentValue.WritesEnabled)).ToArray();
    }

    public async Task<SepayCallbackResponse> CallbackAsync(string state, string? code, string? error, CancellationToken ct)
    {
        if (state.Length != 64 || !state.All(Uri.IsHexDigit)) throw Failure("InvalidState", "Phiên cấp quyền không hợp lệ hoặc đã hết hạn.", 400);
        var value = await redis.GetDatabase().StringGetDeleteAsync(StateKey(state)).WaitAsync(ct);
        if (value.IsNullOrEmpty) throw Failure("InvalidState", "Phiên cấp quyền không hợp lệ hoặc đã dùng. Hãy bắt đầu kết nối lại.", 400);
        var auth = JsonSerializer.Deserialize<AuthorizationState>(value.ToString()) ?? throw Failure("InvalidState", "Phiên không hợp lệ.", 400);
        return await Locked("club:" + auth.GroupId, async token =>
        {
            var app = await ReadyApplication(token);
            var bank = await Bank(auth.GroupId, auth.Actor, token);
            if (app.Revision != auth.Revision || bank.Id != auth.BankAccountId) throw Failure("ConfigurationChanged", "Cấu hình đã thay đổi. Hãy kết nối lại.");
            if (!string.IsNullOrEmpty(error) || string.IsNullOrWhiteSpace(code)) return new SepayCallbackResponse(app.ClubReturnUrl(auth.GroupId) + "?sepay=denied");
            if (code.Length > 4096) throw Failure("InvalidCode", "Mã cấp quyền không hợp lệ.", 400);
            var existing = await repository.GetConnectionAsync(bank.Id, token);
            if (existing?.Status is "CONNECTED" or "PROVISIONING" or "DISCONNECTING") throw Failure("AlreadyConnected", "CLB đã có kết nối. Vui lòng tải lại.");
            var tokens = await provider.ExchangeAsync(app.ClientId, Unprotect(app.ProtectedClientSecret), app.CallbackUrl, code, token);
            var company = await provider.CompanyAsync(tokens.AccessToken, token);
            await Manager(auth.GroupId, auth.Actor, token);
            if (existing is not null && existing.Status != "DISCONNECTED")
            {
                existing.Disconnect(auth.Actor, Now);
                await repository.SaveAsync(token);
            }
            repository.AddConnection(ClubSepayConnection.Authorize(bank, company, Protect(JsonSerializer.Serialize(tokens)), app.Revision, auth.Actor, Now));
            await repository.SaveAsync(token);
            return new SepayCallbackResponse(app.ClubReturnUrl(auth.GroupId) + "?sepay=authorized");
        }, ct);
    }

    public Task<IReadOnlyList<SepayBankAccountResponse>> AccountsAsync(Guid groupId, Guid actor, CancellationToken ct) =>
        Locked<IReadOnlyList<SepayBankAccountResponse>>("club:" + groupId, async token =>
        {
            var bank = await Bank(groupId, actor, token);
            var connection = await OwnedConnection(bank, actor, token);
            var access = await Access(connection, token);
            var accounts = await provider.BanksAsync(access, token);
            await Manager(groupId, actor, token);
            return accounts.Select(x => new SepayBankAccountResponse(x.Id, x.AccountNumber, x.AccountHolder,
                x.BankBin, x.Gateway, x.Active, x.Active && x.AccountNumber == bank.AccountNumber && x.BankBin == bank.BankBin)).ToArray();
        }, ct);

    public Task<SepayConnectionResponse> SelectAsync(Guid groupId, Guid actor, long providerBankId, CancellationToken ct) =>
        Locked("club:" + groupId, async token =>
        {
            var bank = await Bank(groupId, actor, token);
            var connection = await OwnedConnection(bank, actor, token);
            if (connection.Status == "PROVISIONING" && connection.ProviderBankAccountId == providerBankId)
                return Connection(bank, connection, await repository.GetApplicationAsync(token));
            var accounts = await provider.BanksAsync(await Access(connection, token), token);
            var selected = accounts.SingleOrDefault(x => x.Id == providerBankId) ?? throw Failure("BankNotFound", "Không tìm thấy tài khoản trong SePay đã cấp quyền.", 404);
            if (await repository.IsProviderBankInUseAsync(connection.CompanyId, selected.Id, connection.Id, token))
                throw Failure("BankAlreadyConnected", "Tài khoản SePay đang gắn với cấu hình ngân hàng khác. Hãy ngắt kết nối cũ trước.");
            connection.SelectBank(bank, selected.Id, selected.AccountNumber, selected.BankBin, selected.Gateway, selected.Active,
                Protect(Convert.ToHexString(RandomNumberGenerator.GetBytes(32))), Now);
            await Manager(groupId, actor, token);
            await repository.SaveAsync(token);
            return Connection(bank, connection, await repository.GetApplicationAsync(token));
        }, ct);

    public Task<SepayConnectionResponse> ActivateAsync(Guid groupId, Guid actor, CancellationToken ct) =>
        Locked("club:" + groupId, async token =>
        {
            var bank = await Bank(groupId, actor, token);
            var connection = await OwnedConnection(bank, actor, token);
            var app = await ReadyApplication(token);
            if (connection.Status == "CONNECTED") return Connection(bank, connection, app);
            if (connection.Status != "PROVISIONING" || connection.ProviderBankAccountId is not long bankId)
                throw Failure("SelectBankRequired", "Cần chọn tài khoản ngân hàng trước.");
            if (app.Revision != connection.ApplicationRevision) throw Failure("ConfigurationChanged", "Cấu hình ứng dụng đã thay đổi. Hãy ngắt kết nối và cấp quyền lại.");
            var access = await Access(connection, token);
            var url = app.PublicApiBaseUrl + "/api/v1/webhooks/sepay/" + bank.Id + "?connectionId=" + connection.Id;
            var secret = Unprotect(connection.ProtectedWebhookSecret);
            var existing = await provider.WebhooksAsync(access, url, token);
            long webhookId;
            if (existing.Count == 0) webhookId = await provider.CreateWebhookAsync(access, bankId, url, secret, token);
            else if (existing.Count == 1 && existing[0] is { Active: true, Authentication: "HMAC_SHA256" } found &&
                found.BankAccountId == bankId && FixedEquals(found.Secret, secret)) webhookId = found.Id;
            else throw Failure("WebhookConflict", "Webhook hiện có không khớp kết nối. Chưa kích hoạt; cần kiểm tra trên SePay.");
            await Manager(groupId, actor, token);
            connection.Connect(webhookId, Now);
            await repository.SaveAsync(token);
            return Connection(bank, connection, app);
        }, ct);

    public Task<SepayConnectionResponse> DisconnectAsync(Guid groupId, Guid actor, Guid? connectionId, CancellationToken ct) =>
        Locked("club:" + groupId, async token =>
        {
            await Manager(groupId, actor, token);
            var selectedConnection = connectionId.HasValue ? await repository.GetConnectionByIdAsync(connectionId.Value, token) : null;
            if (connectionId.HasValue && (selectedConnection is null || selectedConnection.GroupId != groupId))
                throw Failure("NotFound", "Không tìm thấy kết nối của CLB.", 404);
            var bank = selectedConnection is null ? await Bank(groupId, actor, token)
                : await reconciliation.GetBankAsync(selectedConnection.BankAccountId, token)
                    ?? throw Failure("NotFound", "Không tìm thấy ngân hàng của kết nối.", 404);
            var connection = selectedConnection ?? await repository.GetConnectionAsync(bank.Id, token);
            if (connection is null || connection.Status == "DISCONNECTED") return Connection(bank, connection, await repository.GetApplicationAsync(token));
            var authorizedOnly = connection.Status == "AUTHORIZED";
            connection.BeginDisconnect(actor, Now);
            await repository.SaveAsync(token);
            if (authorizedOnly)
            {
                connection.Disconnect(actor, Now);
                await repository.SaveAsync(token);
                return Connection(bank, connection, await repository.GetApplicationAsync(token));
            }
            var access = await Access(connection, token);
            var app = await repository.GetApplicationAsync(token);
            if (connection.ProviderWebhookId is long id) await provider.DeleteWebhookAsync(access, id, token);
            else if (connection.ProviderBankAccountId.HasValue && app is not null)
            {
                var url = app.PublicApiBaseUrl + "/api/v1/webhooks/sepay/" + bank.Id + "?connectionId=" + connection.Id;
                var webhooks = await provider.WebhooksAsync(access, url, token);
                foreach (var webhook in webhooks)
                {
                    if (webhook.BankAccountId != connection.ProviderBankAccountId || !FixedEquals(webhook.Secret, Unprotect(connection.ProtectedWebhookSecret)))
                        throw Failure("WebhookConflict", "Chưa xác minh được webhook để ngắt kết nối.");
                    await provider.DeleteWebhookAsync(access, webhook.Id, token);
                }
            }
            await Manager(groupId, actor, token);
            connection.Disconnect(actor, Now);
            await repository.SaveAsync(token);
            return Connection(bank, connection, app);
        }, ct);

    public async Task<SepayWebhookCredential?> GetAsync(Guid bankAccountId, CancellationToken ct)
    {
        var connection = await repository.GetWebhookConnectionAsync(bankAccountId, ct);
        return connection is { Status: "CONNECTED" }
            ? new(connection.CompanyId.ToString(CultureInfo.InvariantCulture), connection.Gateway, Unprotect(connection.ProtectedWebhookSecret), connection.Id) : null;
    }

    private async Task<string> Access(ClubSepayConnection connection, CancellationToken ct)
    {
        if (connection.Status == "DISCONNECTED") throw Failure("Disconnected", "Kết nối đã ngắt.");
        var tokens = JsonSerializer.Deserialize<SepayTokens>(Unprotect(connection.ProtectedTokens)) ?? throw Failure("AuthorizationRequired", "Cần cấp quyền lại.");
        if (tokens.ExpiresAt > Now.AddMinutes(1)) return tokens.AccessToken;
        var app = await repository.GetApplicationAsync(ct) ?? throw Failure("ApplicationUnavailable", "Ứng dụng chưa được cấu hình.");
        var refreshed = await provider.RefreshAsync(app.ClientId, Unprotect(app.ProtectedClientSecret), tokens.RefreshToken, ct);
        connection.RefreshTokens(Protect(JsonSerializer.Serialize(refreshed)));
        await repository.SaveAsync(ct);
        return refreshed.AccessToken;
    }

    private async Task Admin(Guid actor, CancellationToken ct)
    {
        if (!await repository.CanManageApplicationAsync(actor, ct)) throw Failure("Forbidden", "Bạn không có quyền cấu hình ứng dụng SePay.", 403);
    }
    private async Task Manager(Guid groupId, Guid actor, CancellationToken ct)
    {
        if (!await payments.CanManageBankAsync(groupId, actor, ct)) throw Failure("Forbidden", "Bạn không có quyền quản lý ngân hàng của CLB này.", 403);
    }
    private async Task<GroupBankAccount> Bank(Guid groupId, Guid actor, CancellationToken ct)
    {
        await Manager(groupId, actor, ct);
        var banks = await payments.GetActiveBanksAsync(groupId, false, ct);
        if (banks.Count != 1 || string.IsNullOrWhiteSpace(banks[0].BankBin))
            throw Failure("BankRequired", "Hãy cấu hình đầy đủ ngân hàng nhận tiền và BIN của CLB trước.");
        return banks[0];
    }
    private async Task<SepayApplicationSettings> ReadyApplication(CancellationToken ct)
    {
        var app = await repository.GetApplicationAsync(ct);
        return app is { Enabled: true } ? app : throw Failure("ApplicationUnavailable", "Admin hệ thống chưa bật ứng dụng SePay.");
    }
    private async Task<ClubSepayConnection> OwnedConnection(GroupBankAccount bank, Guid actor, CancellationToken ct)
    {
        var connection = await repository.GetConnectionAsync(bank.Id, ct);
        if (connection is null || connection.Status == "DISCONNECTED") throw Failure("AuthorizationRequired", "Cần kết nối và cấp quyền SePay trước.");
        if (connection.AuthorizedBy != actor && connection.Status != "CONNECTED")
            throw Failure("AuthorizationOwner", "Phiên cấp quyền thuộc người quản lý khác. Hãy ngắt phiên trước khi kết nối lại.", 403);
        return connection;
    }
    private SepayApplicationResponse Application(SepayApplicationSettings? settings) => new(settings?.ClientId ?? "",
        !string.IsNullOrEmpty(settings?.ProtectedClientSecret), settings?.PublicApiBaseUrl ?? "", settings?.FrontendBaseUrl ?? "",
        settings?.CallbackUrl ?? "", settings?.Enabled ?? false, settings?.Revision, operations.CurrentValue.Enabled && operations.CurrentValue.WritesEnabled);
    private SepayConnectionResponse Connection(GroupBankAccount? bank, ClubSepayConnection? connection, SepayApplicationSettings? app) =>
        new(connection?.Id, bank?.Id, bank is null ? "BANK_REQUIRED" : connection?.Status ?? "DISCONNECTED", connection?.CompanyId,
            connection?.Gateway, connection?.ConnectedAt, app?.Enabled == true, operations.CurrentValue.Enabled && operations.CurrentValue.WritesEnabled);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private string Protect(string value) => secrets.Protect(value);
    private string Unprotect(string value)
    {
        try { return secrets.Unprotect(value); }
        catch (CryptographicException) { throw Failure("SecretUnavailable", "Không đọc được khóa kết nối. Cần kiểm tra kho khóa mã hóa của server.", 503); }
    }
    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(
        System.Text.Encoding.UTF8.GetBytes(left), System.Text.Encoding.UTF8.GetBytes(right));
    private static string StateKey(string state) => "sepay:oauth:state:" + Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(state)));
    private static SepaySetupException Failure(string code, string message, int status = 409) => new(code, message, status);

    private async Task<T> Locked<T>(string key, Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        var database = redis.GetDatabase();
        var redisKey = "sepay:setup:lock:" + key;
        var owner = Guid.NewGuid().ToString("N");
        if (!await database.StringSetAsync(redisKey, owner, TimeSpan.FromSeconds(60), When.NotExists).WaitAsync(ct))
            throw Failure("Busy", "Đang xử lý kết nối khác. Vui lòng thử lại.");
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            return await action(deadline.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw Failure("ProviderTimeout", "Kết nối quá thời gian chờ. Vui lòng tải lại trạng thái trước khi thử lại.", 503); }
        finally
        {
            await database.ScriptEvaluateAsync("if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end",
                new RedisKey[] { redisKey }, new RedisValue[] { owner }).WaitAsync(TimeSpan.FromSeconds(2));
        }
    }
}
