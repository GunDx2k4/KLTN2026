using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Domain.Aggregates;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class SessionBooking : AggregateRoot<Guid>
{
    public Guid SessionId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? RecruitmentId { get; private set; }

    /// <summary>
    /// Trạng thái đăng ký của biểu quyết giữ chỗ. Miền giá trị: CONFIRMED, WAITING, ABSENT, CHECKED_IN.
    /// </summary>
    public BookingStatus Status { get; private set; } = BookingStatus.CONFIRMED;

    public int QueueOrder { get; private set; }
    public DateTime BookedAt { get; private set; }
    public DateTime? CheckinAt { get; private set; }

    public virtual BadmintonSession Session { get; private set; } = default!;
    public virtual AppUser User { get; private set; } = default!;
    public virtual GuestRecruitment? Recruitment { get; private set; }

    protected SessionBooking() { }

    public SessionBooking(
        Guid sessionId,
        Guid userId,
        Guid? recruitmentId = null,
        BookingStatus status = BookingStatus.CONFIRMED,
        int queueOrder = 0)
    {
        Id = Guid.NewGuid();
        SessionId = sessionId;
        UserId = userId;
        RecruitmentId = recruitmentId;
        Status = status;
        QueueOrder = queueOrder;
        BookedAt = DateTime.UtcNow;
    }

    public SessionBooking(
        Guid sessionId,
        Guid userId,
        Guid? recruitmentId,
        string status,
        int queueOrder = 0)
        : this(sessionId, userId, recruitmentId, ParseStatus(status), queueOrder)
    {
    }

    public void CheckIn()
    {
        Status = BookingStatus.CHECKED_IN;
        CheckinAt = DateTime.UtcNow;
    }

    public void UpdateStatus(BookingStatus status)
    {
        Status = status;
    }

    public void UpdateStatus(string status)
    {
        UpdateStatus(ParseStatus(status));
    }

    public void UpdateQueueOrder(int order)
    {
        QueueOrder = order;
    }

    private static BookingStatus ParseStatus(string status)
    {
        return Enum.TryParse<BookingStatus>(status, true, out var result) ? result : BookingStatus.CONFIRMED;
    }
}
