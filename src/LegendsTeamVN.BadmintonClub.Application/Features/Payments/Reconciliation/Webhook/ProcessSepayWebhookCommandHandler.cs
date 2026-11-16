using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.Reconciliation.Webhook;

public sealed class ProcessSepayWebhookCommandHandler(IReconciliationRepository repository, ISepayWebhookCredentials credentials, TimeProvider clock,
    IOptionsMonitor<ReconciliationOptions> options, ILogger<ProcessSepayWebhookCommandHandler> logger)
    : ICommandHandler<ProcessSepayWebhookCommand, SepayWebhookResponse>
{
    public async Task<Result<SepayWebhookResponse>> Handle(ProcessSepayWebhookCommand request, CancellationToken ct)
    {
        if (request.Verified is not { } verified)
            return Result.Failure<SepayWebhookResponse>(Error.Unauthorized("Reconciliation.Unverified", "Webhook chưa được xác thực."));
        if (!options.CurrentValue.Enabled || !options.CurrentValue.WritesEnabled) return SepayWebhookSecurityBehavior.Unavailable();
        var currentCredential = await credentials.GetAsync(request.BankAccountId, ct);
        if (currentCredential is null || currentCredential.ConnectionId != verified.ConnectionId ||
            currentCredential.CompanyId != verified.CompanyId || currentCredential.Gateway != verified.Gateway)
            return SepayWebhookSecurityBehavior.Unavailable();
        var evidence = verified.Evidence;
        await repository.LockProviderAsync(evidence.ProviderTransactionId, ct);
        await repository.LockReferenceAsync(evidence.ReferenceCode, ct);
        var stored = await repository.FindProviderAsync(evidence.ProviderTransactionId, verified.CompanyId, ct);
        if (stored is not null)
        {
            if (stored.ProviderCompanyId == verified.CompanyId && stored.BankAccountId == request.BankAccountId &&
                PaymentReconciliationPolicy.Compare(evidence, stored.GetEvidence()) == PaymentEvidenceComparison.RETRY)
                return Result.Success(new SepayWebhookResponse(true));
            logger.LogWarning("Xung đột bằng chứng webhook {Id}.", evidence.ProviderTransactionId);
            return Result.Failure<SepayWebhookResponse>(Error.Conflict("Reconciliation.EvidenceCollision", "Định danh giao dịch đã có bằng chứng khác."));
        }
        var bank = await repository.GetBankAsync(request.BankAccountId, ct);
        if (bank is null || PaymentReconciliationPolicy.CheckBank(bank, verified.Gateway, evidence) != PaymentReconciliationDecision.MATCHED)
            return Result.Failure<SepayWebhookResponse>(Error.Unauthorized("Reconciliation.BankBinding", "Tài khoản nhận đã thay đổi."));
        if (evidence.IsOutgoing) return Result.Success(new SepayWebhookResponse(true));
        var transaction = BankTransaction.Receive(bank.Id, evidence, verified.CompanyId);
        IReadOnlyList<BankTransaction> collisions = evidence.ReferenceCode is null ? [] : await repository.FindReferenceAsync(evidence.ReferenceCode, ct);
        if (transaction.HasReferenceCollision(collisions)) transaction.RequireManualCheck("REFERENCE_COLLISION");
        else
        {
            var codes = PaymentReconciliationPolicy.ExtractPaymentCodes(evidence.Content);
            var feeId = codes.Count == 1 ? await repository.FindFeeByPaymentCodeAsync(bank.GroupId, codes[0], ct) : null;
            if (feeId is Guid id)
            {
                var target = await repository.LoadForConfirmationAsync(bank.GroupId, id, null, evidence.ReferenceCode, ct);
                if (target is { } found)
                {
                    var decision = transaction.Confirm(found.Fee, found.Session, bank, verified.Gateway, clock.GetUtcNow().UtcDateTime);
                    if (decision == PaymentReconciliationDecision.MATCHED)
                        repository.AddLedger(GroupLedger.CreateSessionFeeReceipt(found.Session, found.Fee, transaction));
                }
                else transaction.RequireManualCheck("FEE_UNAVAILABLE");
            }
            else transaction.RequireManualCheck(codes.Count > 1 ? "MULTIPLE_CODES" : codes.Count == 0 ? "MISSING_CODE" : "FEE_NOT_FOUND");
        }
        if (transaction.ReconciliationStatus == ReconciliationStatus.MANUAL_CHECK)
            transaction.RequestManualReview(bank.GroupId, clock.GetUtcNow().UtcDateTime);
        repository.AddTransaction(transaction);
        return Result.Success(new SepayWebhookResponse(true));
    }
}
