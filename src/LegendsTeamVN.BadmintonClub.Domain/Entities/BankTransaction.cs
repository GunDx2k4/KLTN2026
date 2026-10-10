using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Domain.Aggregates;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class BankTransaction : AggregateRoot<Guid>
{
    public Guid? FeeId { get; private set; }
    public string RecipientAccount { get; private set; } = default!;
    public string ReferenceCode { get; private set; } = default!;
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
        string referenceCode,
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
        string referenceCode,
        decimal amountReceived,
        string transferContent,
        DateTime transactionTime,
        Guid? feeId = null,
        string reconciliationStatus = "MATCHED")
        : this(recipientAccount, referenceCode, amountReceived, transferContent, transactionTime, feeId, ParseReconciliationStatus(reconciliationStatus))
    {
    }

    public void MatchFee(Guid feeId)
    {
        FeeId = feeId;
        ReconciliationStatus = ReconciliationStatus.MATCHED;
    }

    public void UpdateReconciliationStatus(ReconciliationStatus status)
    {
        ReconciliationStatus = status;
    }

    public void UpdateReconciliationStatus(string status)
    {
        ReconciliationStatus = ParseReconciliationStatus(status);
    }

    private static ReconciliationStatus ParseReconciliationStatus(string status)
    {
        return Enum.TryParse<ReconciliationStatus>(status, true, out var result) ? result : ReconciliationStatus.MATCHED;
    }
}
