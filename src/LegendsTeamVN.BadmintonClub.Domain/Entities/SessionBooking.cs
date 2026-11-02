using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Domain.Aggregates;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class SessionBooking : AggregateRoot<Guid>
{
    public Guid SessionId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? RecruitmentId { get; private set; }

    /// <summary>
    /// Trạng thái đăng ký và điểm danh: CONFIRMED, WAITING, ABSENT, CHECKED_IN, NO_SHOW.
    /// </summary>
    public BookingStatus Status { get; private set; } = BookingStatus.CONFIRMED;

    public int QueueOrder { get; private set; }
    public DateTime BookedAt { get; private set; }
    public DateTime? CheckinAt { get; private set; }
    public DateTime? AttendanceMarkedAt { get; private set; }
    public Guid? AttendanceMarkedBy { get; private set; }
    public bool IsRotationPaused { get; private set; }
    public RotationPauseSource? RotationPauseSource { get; private set; }
    public Guid? SelfPauseTargetMatchId { get; private set; }

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
        if (Status == BookingStatus.CHECKED_IN && CheckinAt.HasValue) return;
        Status = BookingStatus.CHECKED_IN;
        CheckinAt ??= DateTime.UtcNow;
        QueueOrder = 0;
    }

    public void MarkPresent(Guid actorId, DateTime now)
    {
        if (Status == BookingStatus.CHECKED_IN && !IsDeleted) return;
        IsDeleted = false;
        DeletedOnUtc = null;
        DeletedBy = null;
        Status = BookingStatus.CHECKED_IN;
        CheckinAt = now;
        QueueOrder = 0;
        AttendanceMarkedAt = now;
        AttendanceMarkedBy = actorId;
    }

    public bool OccupiesSlot() => !IsDeleted && Status is BookingStatus.CONFIRMED or BookingStatus.CHECKED_IN;

    public void MarkNoShow(Guid actorId, DateTime now)
    {
        if (Status == BookingStatus.NO_SHOW) return;
        if (Status is not (BookingStatus.CONFIRMED or BookingStatus.CHECKED_IN))
            throw new InvalidOperationException("Only an official participant can be marked no-show.");
        Status = BookingStatus.NO_SHOW;
        CheckinAt = null;
        QueueOrder = 0;
        AttendanceMarkedAt = now;
        AttendanceMarkedBy = actorId;
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

    public void AssignRecruitment(Guid? recruitmentId) => RecruitmentId = recruitmentId;

    internal void SetRotationPaused(bool isPaused, RotationPauseSource source)
    {
        IsRotationPaused = isPaused;
        RotationPauseSource = isPaused ? source : null;
        SelfPauseTargetMatchId = null;
    }

    internal void BindSelfPauseTarget(Guid matchId)
    {
        if (!IsRotationPaused || RotationPauseSource != Enums.RotationPauseSource.Self ||
            SelfPauseTargetMatchId.HasValue || matchId == Guid.Empty)
            throw new InvalidOperationException("Only an unbound self pause can acquire a target.");
        SelfPauseTargetMatchId = matchId;
    }

    internal bool HasValidPauseMetadata() =>
        (!RotationPauseSource.HasValue || Enum.IsDefined(RotationPauseSource.Value)) &&
        (IsRotationPaused || (!RotationPauseSource.HasValue && !SelfPauseTargetMatchId.HasValue)) &&
        (!SelfPauseTargetMatchId.HasValue || IsRotationPaused &&
            RotationPauseSource == Enums.RotationPauseSource.Self && SelfPauseTargetMatchId != Guid.Empty);

    private static BookingStatus ParseStatus(string status)
    {
        return Enum.TryParse<BookingStatus>(status, true, out var result) ? result : BookingStatus.CONFIRMED;
    }
}
