using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Domain.Aggregates;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class GuestRecruitment : AggregateRoot<Guid>
{
    public Guid SessionId { get; private set; }
    public int SlotsNeeded { get; private set; }
    public SkillLevel MinSkillLevel { get; private set; } = SkillLevel.NB;
    public decimal SharedCost { get; private set; }
    public decimal ScanRadiusKm { get; private set; }

    /// <summary>
    /// Trạng thái tìm kiếm của yêu cầu tìm giao lưu. Miền giá trị: ACTIVE, FILLED, EXPIRED.
    /// </summary>
    public RecruitmentStatus Status { get; private set; } = RecruitmentStatus.ACTIVE;

    public DateTime BroadcastAt { get; private set; }

    public virtual BadmintonSession Session { get; private set; } = default!;
    public virtual ICollection<SessionBooking> Bookings { get; private set; } = new List<SessionBooking>();

    protected GuestRecruitment() { }

    public GuestRecruitment(
        Guid sessionId,
        int slotsNeeded,
        SkillLevel minSkillLevel = SkillLevel.NB,
        decimal sharedCost = 0,
        decimal scanRadiusKm = 5.0m,
        RecruitmentStatus status = RecruitmentStatus.ACTIVE)
    {
        Id = Guid.NewGuid();
        SessionId = sessionId;
        SlotsNeeded = slotsNeeded;
        MinSkillLevel = minSkillLevel;
        SharedCost = sharedCost;
        ScanRadiusKm = scanRadiusKm;
        Status = status;
        BroadcastAt = DateTime.UtcNow;
    }

    public GuestRecruitment(
        Guid sessionId,
        int slotsNeeded,
        SkillLevel minSkillLevel,
        decimal sharedCost,
        decimal scanRadiusKm,
        string status)
        : this(sessionId, slotsNeeded, minSkillLevel, sharedCost, scanRadiusKm, ParseStatus(status))
    {
    }

    public void UpdateRecruitment(int slotsNeeded, SkillLevel minSkillLevel, decimal sharedCost, decimal scanRadiusKm)
    {
        SlotsNeeded = slotsNeeded;
        MinSkillLevel = minSkillLevel;
        SharedCost = sharedCost;
        ScanRadiusKm = scanRadiusKm;
    }

    public void UpdateStatus(RecruitmentStatus status)
    {
        Status = status;
    }

    public void UpdateStatus(string status)
    {
        UpdateStatus(ParseStatus(status));
    }

    private static RecruitmentStatus ParseStatus(string status)
    {
        return Enum.TryParse<RecruitmentStatus>(status, true, out var result) ? result : RecruitmentStatus.ACTIVE;
    }
}
