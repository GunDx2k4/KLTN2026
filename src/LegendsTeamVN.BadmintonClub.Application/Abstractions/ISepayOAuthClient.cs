namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;

public sealed record SepayTokens(string AccessToken, string RefreshToken, DateTime ExpiresAt)
{
    public override string ToString() => nameof(SepayTokens);
}
public sealed record SepayRemoteBank(long Id, string AccountNumber, string AccountHolder, string BankBin, string Gateway, bool Active);
public sealed record SepayRemoteWebhook(long Id, string Url, long BankAccountId, string Authentication, string Secret, bool Active)
{
    public override string ToString() => nameof(SepayRemoteWebhook);
}

public interface ISepayOAuthClient
{
    Task<SepayTokens> ExchangeAsync(string clientId, string secret, string callbackUrl, string code, CancellationToken ct);
    Task<SepayTokens> RefreshAsync(string clientId, string secret, string refreshToken, CancellationToken ct);
    Task<long> CompanyAsync(string accessToken, CancellationToken ct);
    Task<IReadOnlyList<SepayRemoteBank>> BanksAsync(string accessToken, CancellationToken ct);
    Task<IReadOnlyList<SepayRemoteWebhook>> WebhooksAsync(string accessToken, string url, CancellationToken ct);
    Task<long> CreateWebhookAsync(string accessToken, long bankId, string url, string secret, CancellationToken ct);
    Task DeleteWebhookAsync(string accessToken, long webhookId, CancellationToken ct);
}
