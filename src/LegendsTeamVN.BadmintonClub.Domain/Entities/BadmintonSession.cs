using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Domain.Aggregates;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class BadmintonSession : AggregateRoot<Guid>
{
    public Guid GroupId { get; private set; }
    public string Title { get; private set; } = default!;
    public DateTime StartTime { get; private set; }
    public DateTime EndTime { get; private set; }
    public string CourtLocation { get; private set; } = default!;
    public int CourtCount { get; private set; }
    public int TargetPlayerPerCourt { get; private set; }
    public int MaxPlayers { get; private set; }
    public DateTime DeadlineBooking { get; private set; }
    public int ShuttlecockUsed { get; private set; }
    public decimal CourtRentalCost { get; private set; }
    public decimal ExtraExpense { get; private set; }

    /// <summary>
    /// Trạng thái buổi chơi của buổi sinh hoạt. Miền giá trị: OPEN, LOCKED, PLAYING, CALCULATED, COMPLETED.
    /// </summary>
    public SessionStatus Status { get; private set; } = SessionStatus.OPEN;

    public virtual ClubGroup Group { get; private set; } = default!;
    public decimal FeePerMember { get; private set; }

    public virtual ICollection<SessionBooking> Bookings { get; private set; } = new List<SessionBooking>();
    public virtual ICollection<GuestRecruitment> Recruitments { get; private set; } = new List<GuestRecruitment>();
    public virtual ICollection<SessionMatch> Matches { get; private set; } = new List<SessionMatch>();
    public virtual ICollection<SessionFee> Fees { get; private set; } = new List<SessionFee>();
    public virtual ICollection<GroupLedger> LedgerTransactions { get; private set; } = new List<GroupLedger>();

    protected BadmintonSession() { }

    public BadmintonSession(
        Guid groupId,
        string title,
        DateTime startTime,
        DateTime endTime,
        string courtLocation,
        int courtCount = 1,
        int targetPlayerPerCourt = 6,
        int maxPlayers = 6,
        DateTime? deadlineBooking = null,
        int shuttlecockUsed = 0,
        decimal courtRentalCost = 0,
        decimal extraExpense = 0,
        SessionStatus status = SessionStatus.OPEN)
    {
        Id = Guid.NewGuid();
        GroupId = groupId;
        Title = title;
        StartTime = startTime;
        EndTime = endTime;
        CourtLocation = courtLocation;
        CourtCount = courtCount;
        TargetPlayerPerCourt = targetPlayerPerCourt;
        MaxPlayers = maxPlayers;
        DeadlineBooking = deadlineBooking ?? startTime.AddHours(-2);
        ShuttlecockUsed = shuttlecockUsed;
        CourtRentalCost = courtRentalCost;
        ExtraExpense = extraExpense;
        Status = status;
    }

    public BadmintonSession(
        Guid groupId,
        string title,
        DateTime startTime,
        DateTime endTime,
        string courtLocation,
        int courtCount,
        int targetPlayerPerCourt,
        int maxPlayers,
        DateTime? deadlineBooking,
        int shuttlecockUsed,
        decimal courtRentalCost,
        decimal extraExpense,
        string status)
        : this(
            groupId,
            title,
            startTime,
            endTime,
            courtLocation,
            courtCount,
            targetPlayerPerCourt,
            maxPlayers,
            deadlineBooking,
            shuttlecockUsed,
            courtRentalCost,
            extraExpense,
            ParseStatus(status))
    {
    }

    public BadmintonSession(
        Guid clubId,
        string title,
        DateTime startTime,
        DateTime endTime,
        string? location,
        decimal feePerMember = 0,
        int maxMembers = 12)
        : this(
            clubId,
            title,
            startTime,
            endTime,
            location ?? string.Empty,
            courtCount: 1,
            targetPlayerPerCourt: 6,
            maxPlayers: maxMembers)
    {
        FeePerMember = feePerMember;
    }

    public void UpdateSession(
        string title,
        DateTime startTime,
        DateTime endTime,
        string courtLocation,
        int courtCount,
        int maxPlayers,
        DateTime deadlineBooking)
    {
        Title = title;
        StartTime = startTime;
        EndTime = endTime;
        CourtLocation = courtLocation;
        CourtCount = courtCount;
        MaxPlayers = maxPlayers;
        DeadlineBooking = deadlineBooking;
    }

    public void UpdateExpenses(int shuttlecockUsed, decimal courtRentalCost, decimal extraExpense)
    {
        ShuttlecockUsed = shuttlecockUsed;
        CourtRentalCost = courtRentalCost;
        ExtraExpense = extraExpense;
    }

    public void UpdateStatus(SessionStatus status)
    {
        Status = status;
    }

    public void UpdateStatus(string status)
    {
        UpdateStatus(ParseStatus(status));
    }

    private static SessionStatus ParseStatus(string status)
    {
        return Enum.TryParse<SessionStatus>(status, true, out var result) ? result : SessionStatus.OPEN;
    }
}
