namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;

public sealed record SepayWebhookCredential(string CompanyId, string Gateway, string Secret, Guid ConnectionId = default);

public interface ISepayWebhookCredentials
{
    Task<SepayWebhookCredential?> GetAsync(Guid bankAccountId, CancellationToken ct);
}
