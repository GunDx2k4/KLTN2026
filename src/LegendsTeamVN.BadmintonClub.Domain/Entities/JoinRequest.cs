using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Domain.Aggregates;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class JoinRequest : AggregateRoot<Guid>
{
    public Guid GroupId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public string? Introduction { get; private set; }

    /// <summary>
    /// Trạng thái xét duyệt của yêu cầu gia nhập. Miền giá trị: PENDING, APPROVED, REJECTED.
    /// </summary>
    public JoinRequestStatus Status { get; private set; } = JoinRequestStatus.PENDING;

    public DateTime CreatedAt { get; private set; }
    public DateTime? ReviewedAt { get; private set; }

    public virtual ClubGroup Group { get; private set; } = default!;
    public virtual AppUser User { get; private set; } = default!;
    public virtual AppUser? Approver { get; private set; }

    protected JoinRequest() { }

    public JoinRequest(
        Guid groupId,
        Guid userId,
        string? introduction = null,
        JoinRequestStatus status = JoinRequestStatus.PENDING)
    {
        Id = Guid.NewGuid();
        GroupId = groupId;
        UserId = userId;
        Introduction = introduction;
        Status = status;
        CreatedAt = DateTime.UtcNow;
    }

    public JoinRequest(
        Guid groupId,
        Guid userId,
        string? introduction,
        string status)
        : this(groupId, userId, introduction, ParseStatus(status))
    {
    }

    public void Approve(Guid approvedBy)
    {
        Status = JoinRequestStatus.APPROVED;
        ApprovedBy = approvedBy;
        ReviewedAt = DateTime.UtcNow;
    }

    public void Reject(Guid reviewedBy)
    {
        Status = JoinRequestStatus.REJECTED;
        ApprovedBy = reviewedBy;
        ReviewedAt = DateTime.UtcNow;
    }

    private static JoinRequestStatus ParseStatus(string status)
    {
        return Enum.TryParse<JoinRequestStatus>(status, true, out var result) ? result : JoinRequestStatus.PENDING;
    }
}
