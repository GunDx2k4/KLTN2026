using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Domain.Aggregates;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class GroupLedger : AggregateRoot<Guid>
{
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

    public void UpdateCategory(LedgerCategory category)
    {
        Category = category;
    }

    public void UpdateCategory(string category)
    {
        Category = ParseLedgerCategory(category);
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
