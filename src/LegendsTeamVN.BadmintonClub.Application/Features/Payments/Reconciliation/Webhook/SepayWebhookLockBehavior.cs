using System.Diagnostics;
using Microsoft.Extensions.Logging;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Exceptions;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.Reconciliation.Webhook;

public sealed class SepayWebhookLockBehavior(IReconciliationLock locks, IServiceScopeFactory scopes, ILogger<SepayWebhookLockBehavior> logger)
    : IPipelineBehavior<ProcessSepayWebhookCommand, Result<SepayWebhookResponse>>
{
    public async Task<Result<SepayWebhookResponse>> Handle(ProcessSepayWebhookCommand request,
        RequestHandlerDelegate<Result<SepayWebhookResponse>> next, CancellationToken ct)
    {
        var verified = request.Verified;
        if (verified is null) return Result.Failure<SepayWebhookResponse>(Error.Unauthorized("Reconciliation.Unverified", "Webhook chưa được xác thực."));
        var timer = Stopwatch.StartNew();
        var lease = await locks.AcquireAsync(verified.Evidence.ProviderTransactionId, ct);
        if (lease is null) return SepayWebhookSecurityBehavior.Unavailable();
        var waitMs = timer.Elapsed.TotalMilliseconds;
        await using (lease)
        {
            try
            {
                var response = await next(ct);
                logger.LogInformation("Webhook {Id}: LockWaitMs={LockWaitMs}, TransactionMs={TransactionMs}",
                    verified.Evidence.ProviderTransactionId, waitMs, timer.Elapsed.TotalMilliseconds - waitMs);
                return response;
            }
            catch (ReconciliationWriteConflictException)
            {
                await using var scope = scopes.CreateAsyncScope();
                var repository = scope.ServiceProvider.GetRequiredService<IReconciliationRepository>();
                var stored = await repository.FindProviderAsync(verified.Evidence.ProviderTransactionId, ct);
                return stored?.BankAccountId == request.BankAccountId &&
                    PaymentReconciliationPolicy.Compare(verified.Evidence, stored.GetEvidence()) == PaymentEvidenceComparison.RETRY
                    ? Result.Success(new SepayWebhookResponse(true)) : SepayWebhookSecurityBehavior.Unavailable();
            }
        }
    }
}
