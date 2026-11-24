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
        var traceId = Activity.Current?.TraceId.ToString();
        try
        {
            if (!options.CurrentValue.Enabled || !options.CurrentValue.WritesEnabled)
                return Reject(Unavailable().Error, "ReconciliationDisabled");
            var verified = await verifier.VerifyAsync(request.BankAccountId, request.Body, request.Signature, request.Timestamp, deadline.Token);
            if (verified.IsFailure) return Reject(verified.Error, "VerificationFailed");
            var bank = await repository.GetBankAsync(request.BankAccountId, deadline.Token);
            if (bank is null) return Reject(Error.Unauthorized("Reconciliation.BankBinding", "Không xác minh được tài khoản nhận."), "BankNotFound");
            if (PaymentReconciliationPolicy.CheckBank(bank, verified.Value.Gateway, verified.Value.Evidence) != PaymentReconciliationDecision.MATCHED)
            {
                await archive.ArchiveAsync(bank.Id, verified.Value.Evidence.ProviderTransactionId, request.Body, deadline.Token);
                return Reject(Error.Unauthorized("Reconciliation.BankBinding", "Bằng chứng không khớp tài khoản nhận."), "BankMismatch");
            }
            request.Verified = verified.Value;
            var authMs = timer.Elapsed.TotalMilliseconds;
            var response = await next(deadline.Token);
            if (response.IsFailure) return Reject(response.Error, "ReconciliationFailed");
            logger.LogInformation("Đã xử lý webhook SePay {Id}. BankAccountId={BankAccountId}, AuthMs={AuthMs}, TotalMs={TotalMs}, TraceId={TraceId}.",
                verified.Value.Evidence.ProviderTransactionId, request.BankAccountId, authMs, timer.Elapsed.TotalMilliseconds, traceId);
            return response;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Không tiếp nhận webhook SePay. BankAccountId={BankAccountId}, ErrorType={ErrorType}, TraceId={TraceId}.",
                request.BankAccountId, ex.GetType().Name, traceId);
            return Unavailable();
        }

        Result<SepayWebhookResponse> Reject(Error error, string reason)
        {
            logger.LogWarning("Webhook SePay bị từ chối. BankAccountId={BankAccountId}, Reason={Reason}, ErrorCode={ErrorCode}, TraceId={TraceId}.",
                request.BankAccountId, reason, error.Code, traceId);
            return Result.Failure<SepayWebhookResponse>(error);
        }
    }

    internal static Result<SepayWebhookResponse> Unavailable() => Result.Failure<SepayWebhookResponse>(
        Error.Failure("Reconciliation.Unavailable", "Chưa thể lưu giao dịch, vui lòng gửi lại."));
}
