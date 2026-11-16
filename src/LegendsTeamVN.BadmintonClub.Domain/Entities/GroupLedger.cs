using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.Core.Utilities.Results;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Domain.Aggregates;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class GroupLedger : AggregateRoot<Guid>
{
    public Guid? MonthlyContributionId { get; private set; }
    public Guid? RequestId { get; private set; }
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
        return Recorded(new GroupLedger(session.GroupId, actor, TransactionType.INFLOW, LedgerCategory.SESSION_FEE,
            fee.Amount, $"Thu phí {fee.PaymentCode}", session.Id)
        {
            FeeId = fee.Id,
            BankTransactionId = transaction?.Id,
            CreatedAt = fee.PaidAt.Value
        });
    }

    public static Result<GroupLedger> CreateExpense(
        Guid groupId, Guid actorId, Guid requestId, decimal amount, LedgerCategory category,
        string description, Services.LedgerBalance balance, DateTime utcNow, Guid? sessionId = null)
    {
        var available = balance.CheckExpense(amount);
        if (available.IsFailure) return Result.Failure<GroupLedger>(available.Error);
        if (groupId == Guid.Empty || actorId == Guid.Empty || requestId == Guid.Empty ||
            utcNow.Kind != DateTimeKind.Utc || sessionId == Guid.Empty ||
            category is not (LedgerCategory.COURT_RENT or LedgerCategory.SHUTTLE_EXPENSE or LedgerCategory.OTHER) ||
            string.IsNullOrWhiteSpace(description) || description.Trim().Length > 1000)
            return Result.Failure<GroupLedger>(Error.Validation("Ledger.InvalidExpense", "Dữ liệu khoản chi không hợp lệ."));
        return Result.Success(Recorded(new GroupLedger(groupId, actorId,
            TransactionType.OUTFLOW, category, amount, description.Trim(), sessionId)
        {
            RequestId = requestId, CreatedAt = utcNow
        }));
    }

    public static Result<GroupLedger> CreateMonthlyReceipt(
        MonthlyContribution contribution, Guid requestId)
    {
        if (contribution.IsDeleted || contribution.Status != MonthlyContributionStatus.PAID ||
            !contribution.PaidAt.HasValue || !contribution.CollectedBy.HasValue ||
            contribution.CollectedBy == Guid.Empty || requestId == Guid.Empty ||
            !Services.LedgerBalance.IsValidAmount(contribution.AmountDue))
            return Result.Failure<GroupLedger>(Error.Conflict("Ledger.InvalidMonthlySource", "Nghĩa vụ tháng chưa đủ điều kiện ghi thu."));
        return Result.Success(Recorded(new GroupLedger(contribution.GroupId,
            contribution.CollectedBy.Value, TransactionType.INFLOW, LedgerCategory.MONTHLY_FUND,
            contribution.AmountDue, $"Thu quỹ tháng {contribution.ContributionMonth:MM/yyyy}")
        {
            RequestId = requestId, MonthlyContributionId = contribution.Id, CreatedAt = contribution.PaidAt.Value
        }));
    }

    private static GroupLedger Recorded(GroupLedger ledger)
    {
        ledger.RaiseDomainEvent(new LedgerRecordedDomainEvent(ledger.Id, ledger.GroupId, ledger.CreatedAt));
        return ledger;
    }

    public bool MatchesExpenseRequest(Guid actorId, decimal amount, LedgerCategory category, string description, Guid? sessionId,
        LedgerAttachment? savedAttachment, string? fileName, string? contentType, byte[]? content) =>
        MonthlyContributionId is null && FeeId is null && TransactionType == TransactionType.OUTFLOW && ActorId == actorId &&
        Amount == amount && Category == category && Description == description.Trim() && SessionId == sessionId &&
        (savedAttachment is null ? content is null && fileName is null && contentType is null :
            content is not null && savedAttachment.Matches(fileName, contentType, content));

    public bool MatchesMonthlyRequest(Guid actorId, Guid monthlyContributionId, decimal expectedAmount) =>
        TransactionType == TransactionType.INFLOW && Category == LedgerCategory.MONTHLY_FUND &&
        MonthlyContributionId == monthlyContributionId && ActorId == actorId && Amount == expectedAmount;

    public void UpdateCategory(LedgerCategory category)
    {
        if (FeeId.HasValue || MonthlyContributionId.HasValue || RequestId.HasValue) throw new InvalidOperationException("Không thể đổi hạng mục của khoản thu có nguồn.");
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
