using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.Reconciliation.Webhook;

public sealed record ProcessSepayWebhookCommand(Guid BankAccountId, byte[] Body, string Signature, string Timestamp)
    : ICommand<SepayWebhookResponse>
{
    public VerifiedSepayWebhook? Verified { get; internal set; }
}
