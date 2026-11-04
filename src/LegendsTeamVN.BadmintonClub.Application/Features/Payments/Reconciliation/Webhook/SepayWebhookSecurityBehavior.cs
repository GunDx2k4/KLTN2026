using System.Diagnostics;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.Reconciliation.Webhook;

public sealed class SepayWebhookSecurityBehavior(ISepayWebhookVerifier verifier, IReconciliationRepository repository,
    IWebhookEvidenceArchive archive, IOptionsMonitor<ReconciliationOptions> options, ILogger<SepayWebhookSecurityBehavior> logger)
    : IPipelineBehavior<ProcessSepayWebhookCommand, Result<SepayWebhookResponse>>
{
    public async Task<Result<SepayWebhookResponse>> Handle(ProcessSepayWebhookCommand request,
        RequestHandlerDelegate<Result<SepayWebhookResponse>> next, CancellationToken ct)
    {
        var timer = Stopwatch.StartNew();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(25));
        request.Verified = null;
        try
        {
            if (!options.CurrentValue.Enabled || !options.CurrentValue.WritesEnabled)
                return Unavailable();
            var verified = await verifier.VerifyAsync(request.BankAccountId, request.Body, request.Signature, request.Timestamp, deadline.Token);
            if (verified.IsFailure) return Result.Failure<SepayWebhookResponse>(verified.Error);
            var bank = await repository.GetBankAsync(request.BankAccountId, deadline.Token);
            if (bank is null) return Result.Failure<SepayWebhookResponse>(Error.Unauthorized("Reconciliation.BankBinding", "Không xác minh được tài khoản nhận."));
            if (PaymentReconciliationPolicy.CheckBank(bank, verified.Value.Gateway, verified.Value.Evidence) != PaymentReconciliationDecision.MATCHED)
            {
                await archive.ArchiveAsync(bank.Id, verified.Value.Evidence.ProviderTransactionId, request.Body, deadline.Token);
                logger.LogWarning("Webhook {Id} không khớp tài khoản nhận; đã cô lập bằng chứng.", verified.Value.Evidence.ProviderTransactionId);
                return Result.Failure<SepayWebhookResponse>(Error.Unauthorized("Reconciliation.BankBinding", "Bằng chứng không khớp tài khoản nhận."));
            }
            request.Verified = verified.Value;
            var authMs = timer.Elapsed.TotalMilliseconds;
            var response = await next(deadline.Token);
            logger.LogInformation("Webhook {Id}: AuthMs={AuthMs}, TotalMs={TotalMs}",
                verified.Value.Evidence.ProviderTransactionId, authMs, timer.Elapsed.TotalMilliseconds);
            return response;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Không tiếp nhận webhook: {Type}", ex.GetType().Name);
            return Unavailable();
        }
    }

    internal static Result<SepayWebhookResponse> Unavailable() => Result.Failure<SepayWebhookResponse>(
        Error.Failure("Reconciliation.Unavailable", "Chưa thể lưu giao dịch, vui lòng gửi lại."));
}
