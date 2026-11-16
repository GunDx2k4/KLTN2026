using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;

public sealed record VerifiedSepayWebhook(PaymentReconciliationEvidence Evidence, string Gateway, string CompanyId, Guid ConnectionId = default);

public interface ISepayWebhookVerifier
{
    Task<Result<VerifiedSepayWebhook>> VerifyAsync(Guid bankAccountId, byte[] body, string signature, string timestamp, CancellationToken ct);
}
