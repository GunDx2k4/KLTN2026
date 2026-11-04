using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;

public sealed record VerifiedSepayWebhook(PaymentReconciliationEvidence Evidence, string Gateway);

public interface ISepayWebhookVerifier
{
    Result<VerifiedSepayWebhook> Verify(Guid bankAccountId, byte[] body, string signature, string timestamp);
}
