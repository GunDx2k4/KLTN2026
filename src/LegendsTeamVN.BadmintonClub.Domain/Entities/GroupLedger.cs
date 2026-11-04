using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Domain.Aggregates;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class GroupLedger : AggregateRoot<Guid>
{
    public Guid? FeeId { get; private set; }
    public Guid? BankTransactionId { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid? SessionId { get; private set; }
    public Guid ActorId { get; private set; }

    /// <summary>
    /// Loại giao dịch của giao dịch quỹ nhóm. Miền giá trị: INFLOW, OUTFLOW.
    /// </summary>
    public TransactionType TransactionType { get; private set; } = TransactionType.INFLOW;

    /// <summary>
    /// Hạng mục giao dịch của giao dịch quỹ nhóm. Miền giá trị: SESSION_FEE, COURT_RENT, SHUTTLE_EXPENSE, MONTHLY_FUND, OTHER.
    /// </summary>
    public LedgerCategory Category { get; private set; } = LedgerCategory.OTHER;

    public decimal Amount { get; private set; }
    public string Description { get; private set; } = default!;
    public DateTime CreatedAt { get; private set; }

    public virtual ClubGroup Group { get; private set; } = default!;
    public virtual BadmintonSession? Session { get; private set; }
    public virtual AppUser Actor { get; private set; } = default!;

    protected GroupLedger() { }

    public GroupLedger(
        Guid groupId,
        Guid actorId,
        TransactionType transactionType,
        LedgerCategory category,
        decimal amount,
        string description,
        Guid? sessionId = null)
    {
        Id = Guid.NewGuid();
        GroupId = groupId;
        ActorId = actorId;
        TransactionType = transactionType;
        Category = category;
        Amount = amount;
        Description = description;
        SessionId = sessionId;
        CreatedAt = DateTime.UtcNow;
    }

    public GroupLedger(
        Guid groupId,
        Guid actorId,
        TransactionType transactionType,
        string category,
        decimal amount,
        string description,
        Guid? sessionId = null)
        : this(groupId, actorId, transactionType, ParseLedgerCategory(category), amount, description, sessionId)
    {
    }

    public GroupLedger(
        Guid groupId,
        Guid actorId,
        string transactionType,
        string category,
        decimal amount,
        string description,
        Guid? sessionId = null)
        : this(groupId, actorId, ParseTransactionType(transactionType), ParseLedgerCategory(category), amount, description, sessionId)
    {
    }

    public static GroupLedger CreateSessionFeeReceipt(BadmintonSession session, SessionFee fee, BankTransaction? transaction = null)
    {
        if (session.Id != fee.SessionId || fee.PaymentStatus != PaymentStatus.PAID || fee.PaidAt is null ||
            fee.Amount <= 0 || fee.IsDeleted || session.IsDeleted)
            throw new InvalidOperationException("Khoản thu chưa đủ điều kiện ghi sổ.");
        Guid actor;
        if (fee.PaymentMethod == PaymentMethod.BANK_TRANSFER)
        {
            if (transaction is null || transaction.FeeId != fee.Id || transaction.ProviderTransactionId is null ||
                transaction.ReconciliationStatus != ReconciliationStatus.MATCHED || transaction.AmountReceived != fee.Amount ||
                transaction.BankAccountId != fee.QrBankAccountId)
                throw new InvalidOperationException("Thiếu bằng chứng ngân hàng khớp khoản thu.");
            actor = transaction.ReconciledBy ?? fee.UserId;
        }
        else if (fee.PaymentMethod == PaymentMethod.CASH && transaction is null && fee.CashCollectedBy is Guid collector)
            actor = collector;
        else throw new InvalidOperationException("Nguồn thu không hợp lệ.");
        if (actor == Guid.Empty) throw new InvalidOperationException("Thiếu người nộp hoặc người xác nhận.");
        return new GroupLedger(session.GroupId, actor, TransactionType.INFLOW, LedgerCategory.SESSION_FEE,
            fee.Amount, $"Thu phí {fee.PaymentCode}", session.Id)
        {
            FeeId = fee.Id,
            BankTransactionId = transaction?.Id,
            CreatedAt = fee.PaidAt.Value
        };
    }

    public void UpdateCategory(LedgerCategory category)
    {
        if (FeeId.HasValue) throw new InvalidOperationException("Không thể đổi hạng mục của khoản thu có nguồn.");
        if (!Enum.IsDefined(category)) throw new ArgumentOutOfRangeException(nameof(category));
        Category = category;
    }

    public void UpdateCategory(string category)
    {
        UpdateCategory(ParseLedgerCategory(category));
    }

    private static TransactionType ParseTransactionType(string transactionType)
    {
        return Enum.TryParse<TransactionType>(transactionType, true, out var result) ? result : TransactionType.INFLOW;
    }

    private static LedgerCategory ParseLedgerCategory(string category)
    {
        return Enum.TryParse<LedgerCategory>(category, true, out var result) ? result : LedgerCategory.OTHER;
    }
}
