using System.Text.RegularExpressions;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.Advance;

public sealed record AdvancePaymentView(Guid? Id, Guid SessionId, Guid GroupId, string SessionTitle, string Status, decimal Amount,
    decimal Received, decimal Applied, decimal RefundDue, string? Code, string? QrPayload,
    DateTime ExpiresAt, DateTime? RefundedAt, string? RefundReason, string? RefundBank, string? RefundAccount,
    string? RefundHolder, string? RefundReference, Guid? ReviewTransactionId, bool CanHandle, bool CanPrepare, bool RefundClaimed, decimal? ReviewAmount = null, string? ReviewReference = null, string? PayerName = null)
{
    public static AdvancePaymentView Map(SessionAdvancePayment p, DateTime now, bool canPrepare, Guid? operatorId = null, bool own = false)
    {
        var status = p.Status == SessionAdvanceStatus.OPEN && !canPrepare ? "CLOSED" : p.Status.ToString();
        var reveal = own || operatorId.HasValue && p.RefundOperatorId == operatorId;
        var account = reveal || p.RefundAccount is null ? p.RefundAccount : "••••" + p.RefundAccount[^4..];
        return new(p.Id, p.SessionId, p.GroupId, p.SessionTitle, status, p.Amount, p.ReceivedAmount, p.AppliedAmount,
            p.RefundDue, p.PaymentCode, own && canPrepare && p.ExpiresAt > now &&
                p.Status == SessionAdvanceStatus.OPEN ? p.QrPayload : null,
            p.ExpiresAt, p.RefundedAt, p.RefundReason, p.RefundBank, account, reveal ? p.RefundHolder : null,
            own || operatorId.HasValue ? p.RefundReference : null, operatorId.HasValue ? p.ReviewTransactionId : null,
            operatorId.HasValue && p.RefundOperatorId == operatorId, canPrepare, p.RefundOperatorId.HasValue);
    }
}

public sealed record MyAdvancesQuery : IQuery<IReadOnlyList<AdvancePaymentView>>;
public sealed record ClubAdvancesQuery(Guid GroupId) : IQuery<IReadOnlyList<AdvancePaymentView>>;
public sealed record SessionAdvanceQuery(Guid SessionId) : IQuery<AdvancePaymentView>;
public sealed record AdvancePrepareCommand(Guid SessionId) : ICommand<AdvancePaymentView>;
public sealed record AdvanceReportCommand(Guid SessionId) : ICommand<AdvancePaymentView>;
public sealed record AdvanceAccountCommand(Guid SessionId, string Bank, string Account, string Holder) : ICommand<AdvancePaymentView>;
public sealed record AdvanceReviewCommand(Guid GroupId, Guid AdvanceId, Guid? TransactionId, bool Dismiss) : ICommand<AdvancePaymentView>;
public sealed record AdvanceRefundCommand(Guid GroupId, Guid AdvanceId, bool Claim, decimal Amount, string? Reference,
    string? Evidence) : ICommand<AdvancePaymentView>;

public sealed class AdvancePaymentService(ISessionAdvanceRepository repo, ICurrentUserService current,
    IGroupAuthorizationService auth, IPaymentRepository fees, IReconciliationRepository reconciliation,
    ISepayWebhookCredentials credentials, IVietQrPayloadGenerator qr, TimeProvider clock,
    IOptionsMonitor<ReconciliationOptions> options)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<Result<IReadOnlyList<AdvancePaymentView>>> Handle(MyAdvancesQuery request, CancellationToken ct)
    {
        if (current.UserId is not Guid user) return Result.Failure<IReadOnlyList<AdvancePaymentView>>(Forbidden());
        var rows = await repo.ListMineAsync(user, ct);
        return Result.Success<IReadOnlyList<AdvancePaymentView>>(rows.Select(a =>
            AdvancePaymentView.Map(a, Now, a.ExpiresAt > Now, own: false)).ToArray());
    }

    public async Task<Result<IReadOnlyList<AdvancePaymentView>>> Handle(ClubAdvancesQuery request, CancellationToken ct)
    {
        if (current.UserId is not Guid actor || !await Allowed(actor, request.GroupId, ct))
            return Result.Failure<IReadOnlyList<AdvancePaymentView>>(Forbidden());
        var rows = await repo.ListClubAsync(request.GroupId, ct);
        var names = await repo.GetPayerNamesAsync(rows.Select(a => a.UserId).Distinct().ToArray(), ct);
        var views = new List<AdvancePaymentView>(rows.Count);
        foreach (var row in rows)
        {
            var view = AdvancePaymentView.Map(row, Now, false, actor) with { PayerName = names.GetValueOrDefault(row.UserId) };
            if (row.ReviewTransactionId is Guid txId)
            {
                var tx = await repo.GetReviewTransactionAsync(txId, row.BankAccountId, ct);
                if (tx is not null) view = view with { ReviewAmount = tx.AmountReceived, ReviewReference = tx.ReferenceCode };
            }
            views.Add(view);
        }
        return Result.Success<IReadOnlyList<AdvancePaymentView>>(views);
    }

    public async Task<Result<AdvancePaymentView>> Handle(SessionAdvanceQuery request, CancellationToken ct) =>
        await PrepareAsync(request.SessionId, false, ct);

    public async Task<Result<AdvancePaymentView>> Handle(AdvancePrepareCommand request, CancellationToken ct) =>
        await PrepareAsync(request.SessionId, true, ct);

    private async Task<Result<AdvancePaymentView>> PrepareAsync(Guid sessionId, bool create, CancellationToken ct)
    {
        if (current.UserId is not Guid user) return Fail(Forbidden());
        var session = await repo.LoadSessionAsync(sessionId, create, ct);
        if (session is null) return Fail(NotFound());
        var (rule, member, gender) = await repo.GetPolicyAsync(session.GroupId, user, ct);
        var now = Now;
        var allowed = member is not null && session.CanAdvance(user, now);
        var existing = session.AdvancePayments.SingleOrDefault(a => a.UserId == user);
        if (existing is not null)
        {
            allowed &= now < existing.ExpiresAt && existing.Status == SessionAdvanceStatus.OPEN;
            if (allowed)
            {
                var activeBanks = await fees.GetActiveBanksAsync(session.GroupId, false, ct);
                allowed = options.CurrentValue.Enabled && options.CurrentValue.WritesEnabled &&
                    activeBanks.Count == 1 && activeBanks[0].Id == existing.BankAccountId &&
                    await credentials.GetAsync(existing.BankAccountId, ct) is not null;
            }
            return Result.Success(AdvancePaymentView.Map(existing, now, allowed, own: true));
        }
        if (!allowed || rule is null || member is null ||
            !BadmintonSession.ValidAdvanceAmount(session.QuoteAdvance(rule, member, gender)))
            return Fail(NotFound());
        var amount = session.QuoteAdvance(rule, member, gender);
        var banks = await fees.GetActiveBanksAsync(session.GroupId, false, ct);
        if (!options.CurrentValue.Enabled || !options.CurrentValue.WritesEnabled ||
            banks.Count != 1 || await credentials.GetAsync(banks[0].Id, ct) is null)
            return Fail(Error.Conflict("Advance.BankUnavailable", "Đối soát khoản ứng đang tạm dừng."));
        if (!create) return Result.Success(new AdvancePaymentView(null, session.Id, session.GroupId, session.Title, "AVAILABLE",
            amount, 0, 0, 0, null, null, session.StartTime, null, null, null, null, null, null, null, false, true, false));
        var bank = banks[0];
        var key = Guid.NewGuid().ToString("N").ToUpperInvariant();
        var code = $"UNG {key[..12]} {key[12..24]}";
        var payload = qr.Generate(new PaymentBankSnapshot(bank.Id, bank.BankBin!, bank.BankName, bank.AccountNumber, bank.AccountHolder), amount, code);
        if (payload.IsFailure) return Fail(payload.Error);
        var payment = new SessionAdvancePayment(session, user, bank.Id, amount, code, payload.Value);
        repo.Add(payment);
        return Result.Success(AdvancePaymentView.Map(payment, now, true, own: true));
    }

    public async Task<Result<AdvancePaymentView>> Handle(AdvanceReportCommand request, CancellationToken ct)
    {
        if (current.UserId is not Guid user) return Fail(Forbidden());
        var session = await repo.LoadSessionAsync(request.SessionId, true, ct);
        var advance = session?.AdvancePayments.SingleOrDefault(a => a.UserId == user);
        if (session is null || advance is null) return Fail(NotFound());
        if (!session.CanAdvance(user, Now) || !advance.ReportTransfer())
            return Fail(Error.Conflict("Advance.ReportUnavailable", "Khoản ứng đã hết hạn hoặc đã được ghi nhận."));
        return Result.Success(AdvancePaymentView.Map(advance, Now, false, own: true));
    }

    public async Task<Result<AdvancePaymentView>> Handle(AdvanceAccountCommand request, CancellationToken ct)
    {
        if (current.UserId is not Guid user) return Fail(Forbidden());
        var session = await repo.LoadSessionAsync(request.SessionId, true, ct);
        var advance = session?.AdvancePayments.SingleOrDefault(a => a.UserId == user);
        if (advance is null) return Fail(NotFound());
        var result = advance.SetRefundAccount(request.Bank ?? "", request.Account ?? "", request.Holder ?? "");
        return result.IsFailure ? Fail(result.Error) : Result.Success(AdvancePaymentView.Map(advance, Now, false, own: true));
    }

    public async Task<Result<AdvancePaymentView>> Handle(AdvanceReviewCommand request, CancellationToken ct)
    {
        if (current.UserId is not Guid actor || !await Allowed(actor, request.GroupId, ct)) return Fail(Forbidden());
        var session = await GetClubSession(request.GroupId, request.AdvanceId, ct);
        var advance = session?.AdvancePayments.SingleOrDefault(a => a.Id == request.AdvanceId);
        if (session is null || advance is null) return Fail(NotFound());
        if (request.Dismiss)
        {
            if (!advance.DismissReview()) return Fail(Error.Conflict("Advance.ReviewHasEvidence", "Khoản ứng còn bằng chứng cần xử lý."));
        }
        else
        {
            if (request.TransactionId is not Guid id) return Fail(NotFound());
            var transaction = await repo.LockTransactionAsync(id, ct);
            if (transaction is null || transaction.BankAccountId != advance.BankAccountId ||
                transaction.ReferenceCode is string reference && transaction.HasReferenceCollision(await reconciliation.FindReferenceAsync(reference, ct)) ||
                PaymentReconciliationPolicy.ExtractPaymentCodes(transaction.TransferContent).Count != 0 ||
                !AdvancePaymentCodes.Extract(transaction.TransferContent).SequenceEqual([advance.PaymentCode]) ||
                !transaction.ConfirmAdvance(advance, session, Now, actor))
                return Fail(Error.Conflict("Advance.EvidenceMismatch", "Bằng chứng không khớp với khoản ứng."));
            repo.Add(GroupLedger.RecordAdvance(advance, actor, transaction.Id, Now));
        }
        session.Fees.SingleOrDefault(f => f.UserId == advance.UserId)?.SetAdvancePending(false);
        return Result.Success(AdvancePaymentView.Map(advance, Now, false, actor));
    }

    public async Task<Result<AdvancePaymentView>> Handle(AdvanceRefundCommand request, CancellationToken ct)
    {
        if (current.UserId is not Guid actor || !await Allowed(actor, request.GroupId, ct)) return Fail(Forbidden());
        var session = await GetClubSession(request.GroupId, request.AdvanceId, ct);
        var advance = session?.AdvancePayments.SingleOrDefault(a => a.Id == request.AdvanceId);
        if (advance is null) return Fail(NotFound());
        var reference = (request.Reference ?? "").Trim().ToUpperInvariant();
        if (!request.Claim && await repo.HasRefundReferenceAsync(request.GroupId, reference, advance.Id, ct))
            return Fail(Error.Conflict("Advance.DuplicateRefund", "Mã giao dịch hoàn đã dùng."));
        var wasRefunded = advance.Status == SessionAdvanceStatus.REFUNDED;
        var result = request.Claim ? advance.ClaimRefund(actor) :
            advance.ConfirmRefund(actor, request.Amount, reference, request.Evidence ?? "", Now);
        if (result.IsFailure) return Fail(result.Error);
        if (!request.Claim && !wasRefunded) repo.Add(GroupLedger.RecordAdvance(advance, actor, null, Now));
        return Result.Success(AdvancePaymentView.Map(advance, Now, false, actor));
    }

    private async Task<BadmintonSession?> GetClubSession(Guid group, Guid advance, CancellationToken ct) =>
        await repo.FindSessionAsync(group, advance, ct) is Guid session ? await repo.LoadSessionAsync(session, true, ct) : null;

    private Task<bool> Allowed(Guid user, Guid group, CancellationToken ct) =>
        auth.HasPermissionAsync(user, group, GroupPermissions.Finance.LedgerManage, ct);

    private static Result<AdvancePaymentView> Fail(Error error) => Result.Failure<AdvancePaymentView>(error);
    private static Error Forbidden() => Error.Forbidden("Advance.Forbidden", "Bạn không có quyền xử lý khoản ứng.");
    private static Error NotFound() => Error.NotFound("Advance.NotAvailable", "Không có khoản ứng cho buổi chơi này.");
}

public sealed class AdvancePaymentQueryHandler(AdvancePaymentService service)
    : IQueryHandler<MyAdvancesQuery, IReadOnlyList<AdvancePaymentView>>,
      IQueryHandler<ClubAdvancesQuery, IReadOnlyList<AdvancePaymentView>>,
      IQueryHandler<SessionAdvanceQuery, AdvancePaymentView>
{
    public Task<Result<IReadOnlyList<AdvancePaymentView>>> Handle(MyAdvancesQuery request, CancellationToken ct) => service.Handle(request, ct);
    public Task<Result<IReadOnlyList<AdvancePaymentView>>> Handle(ClubAdvancesQuery request, CancellationToken ct) => service.Handle(request, ct);
    public Task<Result<AdvancePaymentView>> Handle(SessionAdvanceQuery request, CancellationToken ct) => service.Handle(request, ct);
}

public sealed class AdvancePaymentCommandHandler(AdvancePaymentService service)
    : ICommandHandler<AdvancePrepareCommand, AdvancePaymentView>,
      ICommandHandler<AdvanceReportCommand, AdvancePaymentView>,
      ICommandHandler<AdvanceAccountCommand, AdvancePaymentView>,
      ICommandHandler<AdvanceReviewCommand, AdvancePaymentView>,
      ICommandHandler<AdvanceRefundCommand, AdvancePaymentView>
{
    public Task<Result<AdvancePaymentView>> Handle(AdvancePrepareCommand request, CancellationToken ct) => service.Handle(request, ct);
    public Task<Result<AdvancePaymentView>> Handle(AdvanceReportCommand request, CancellationToken ct) => service.Handle(request, ct);
    public Task<Result<AdvancePaymentView>> Handle(AdvanceAccountCommand request, CancellationToken ct) => service.Handle(request, ct);
    public Task<Result<AdvancePaymentView>> Handle(AdvanceReviewCommand request, CancellationToken ct) => service.Handle(request, ct);
    public Task<Result<AdvancePaymentView>> Handle(AdvanceRefundCommand request, CancellationToken ct) => service.Handle(request, ct);
}

public static class AdvancePaymentCodes
{
    private static readonly Regex Pattern = new(@"(?<![\p{L}\p{N}_])UNG[ \t]+([0-9A-F]{12})[ \t]+([0-9A-F]{12})(?![\p{L}\p{N}_])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public static string[] Extract(string content) =>
        Pattern.Matches(content).Select(m => $"UNG {m.Groups[1].Value.ToUpperInvariant()} {m.Groups[2].Value.ToUpperInvariant()}").ToArray();
}
