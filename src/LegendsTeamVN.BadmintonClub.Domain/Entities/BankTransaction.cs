using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Domain.Aggregates;
using LegendsTeamVN.BadmintonClub.Domain.Services;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class BankTransaction : AggregateRoot<Guid>
{
    public long? ProviderTransactionId { get; private set; }
    public string? ProviderCompanyId { get; private set; }
    public Guid? BankAccountId { get; private set; }
    public string? Gateway { get; private set; }
    public string? SubAccount { get; private set; }
    public string? TransferType { get; private set; }
    public string? ReasonCode { get; private set; }
    public Guid? ReconciledBy { get; private set; }
    public DateTime? ReconciledAt { get; private set; }
    public string? ReconciliationNote { get; private set; }

    public Guid? FeeId { get; private set; }
    public string RecipientAccount { get; private set; } = default!;
    public string? ReferenceCode { get; private set; } = default!;
    public decimal AmountReceived { get; private set; }
    public string TransferContent { get; private set; } = default!;
    public DateTime TransactionTime { get; private set; }

    /// <summary>
    /// Trạng thái đối soát: MATCHED (tự động khớp lệnh), MANUAL_CHECK (sai cú pháp/thiếu tiền).
    /// </summary>
    public ReconciliationStatus ReconciliationStatus { get; private set; } = ReconciliationStatus.MATCHED;

    public virtual SessionFee? SessionFee { get; private set; }

    protected BankTransaction() { }

    public BankTransaction(
        string recipientAccount,
        string? referenceCode,
        decimal amountReceived,
        string transferContent,
        DateTime transactionTime,
        Guid? feeId = null,
        ReconciliationStatus reconciliationStatus = ReconciliationStatus.MATCHED)
    {
        Id = Guid.NewGuid();
        RecipientAccount = recipientAccount;
        ReferenceCode = referenceCode;
        AmountReceived = amountReceived;
        TransferContent = transferContent;
        TransactionTime = transactionTime;
        FeeId = feeId;
        ReconciliationStatus = reconciliationStatus;
    }

    public BankTransaction(
        string recipientAccount,
        string? referenceCode,
        decimal amountReceived,
        string transferContent,
        DateTime transactionTime,
        Guid? feeId = null,
        string reconciliationStatus = "MATCHED")
        : this(recipientAccount, referenceCode, amountReceived, transferContent, transactionTime, feeId, ParseReconciliationStatus(reconciliationStatus))
    {
    }

    public static BankTransaction Receive(Guid bankAccountId, PaymentReconciliationEvidence evidence, string? companyId = null)
    {
        if (bankAccountId == Guid.Empty) throw new ArgumentException("Tài khoản nhận không hợp lệ.", nameof(bankAccountId));
        if (companyId is not null && (string.IsNullOrWhiteSpace(companyId) || companyId.Length > 100))
            throw new ArgumentException("Company SePay không hợp lệ.", nameof(companyId));
        return new BankTransaction(evidence.AccountNumber, evidence.ReferenceCode, evidence.Amount, evidence.Content,
            evidence.TransactionTime, reconciliationStatus: ReconciliationStatus.MANUAL_CHECK)
        {
            ProviderTransactionId = evidence.ProviderTransactionId,
            ProviderCompanyId = companyId,
            BankAccountId = bankAccountId,
            Gateway = evidence.Gateway,
            SubAccount = evidence.SubAccount,
            TransferType = evidence.TransferType,
            ReasonCode = "AWAITING_RECONCILIATION"
        };
    }

    public PaymentReconciliationEvidence? GetEvidence() =>
        ProviderTransactionId is long id && Gateway is string gateway && TransferType is string direction
            ? new(id, gateway, RecipientAccount, SubAccount, direction, AmountReceived, TransactionTime, ReferenceCode, TransferContent)
            : null;

    public bool HasReferenceCollision(IEnumerable<BankTransaction> transactions) =>
        !string.IsNullOrEmpty(ReferenceCode) && transactions.Any(t => t.Id != Id && t.ReferenceCode == ReferenceCode &&
            (ProviderCompanyId is null || t.ProviderCompanyId is null || t.ProviderCompanyId == ProviderCompanyId));

    public void RequireManualCheck(string reasonCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        if (reasonCode.Length > 100) throw new ArgumentOutOfRangeException(nameof(reasonCode));
        if (ProviderTransactionId is null || ReconciliationStatus == ReconciliationStatus.MATCHED)
            throw new InvalidOperationException("Không thể thay đổi bằng chứng cũ hoặc giao dịch đã đối soát.");
        ReasonCode = reasonCode;
    }

    public PaymentReconciliationDecision Confirm(SessionFee fee, BadmintonSession session, GroupBankAccount bank,
        string expectedGateway, DateTime now, Guid? actor = null, string? reason = null)
    {
        var evidence = GetEvidence() ?? throw new InvalidOperationException("Giao dịch không có bằng chứng provider.");
        if (BankAccountId != bank.Id) return PaymentReconciliationDecision.BANK_MISMATCH;
        if (ReconciliationStatus == ReconciliationStatus.MATCHED)
            return FeeId == fee.Id && fee.PaymentStatus == PaymentStatus.PAID
                ? PaymentReconciliationDecision.MATCHED : PaymentReconciliationDecision.ALREADY_PAID;
        if (reason is { Length: > 2000 }) throw new ArgumentOutOfRangeException(nameof(reason));
        var decision = fee.ConfirmBankPayment(session, bank, expectedGateway, evidence, now, actor, reason);
        if (decision != PaymentReconciliationDecision.MATCHED)
        {
            RequireManualCheck(decision.ToString());
            return decision;
        }
        FeeId = fee.Id;
        ReconciliationStatus = ReconciliationStatus.MATCHED;
        ReasonCode = null;
        ReconciledBy = actor;
        ReconciledAt = fee.PaidAt;
        ReconciliationNote = reason;
        return decision;
    }

    public void RequestManualReview(Guid groupId, DateTime now)
    {
        if (ReconciliationStatus != ReconciliationStatus.MANUAL_CHECK || groupId == Guid.Empty || now.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("Giao dịch không đủ điều kiện yêu cầu đối soát.");
        RaiseDomainEvent(new Events.ReconciliationRequiredDomainEvent(Id, groupId, now));
    }

    public void MatchFee(Guid feeId)
    {
        if (ProviderTransactionId.HasValue) throw new InvalidOperationException("Hãy xác nhận bằng chứng qua Confirm.");
        FeeId = feeId;
        ReconciliationStatus = ReconciliationStatus.MATCHED;
    }

    public void UpdateReconciliationStatus(ReconciliationStatus status)
    {
        if (ProviderTransactionId.HasValue) throw new InvalidOperationException("Không thể đặt trực tiếp trạng thái giao dịch provider.");
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        ReconciliationStatus = status;
    }

    public void UpdateReconciliationStatus(string status)
    {
        UpdateReconciliationStatus(ParseReconciliationStatus(status));
    }

    private static ReconciliationStatus ParseReconciliationStatus(string status)
    {
        return Enum.TryParse<ReconciliationStatus>(status, out var result) && Enum.IsDefined(result) && result.ToString() == status
            ? result : throw new ArgumentException("Trạng thái đối soát không hợp lệ.", nameof(status));
    }
}
