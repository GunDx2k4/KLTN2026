using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Domain.Aggregates;
using LegendsTeamVN.Core.Utilities.Results;

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
    public DateTime? AttendanceCompletedAt { get; private set; }
    public Guid? AttendanceCompletedBy { get; private set; }
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

    public bool CanEditAttendance(DateTime now) =>
        !IsDeleted && StartTime.AddHours(-1) <= now && now < EndTime &&
        Status is SessionStatus.OPEN or SessionStatus.LOCKED or SessionStatus.PLAYING;

    public bool HasAttendanceRoom(SessionBooking? booking) =>
        (booking is not null && booking.SessionId == Id && booking.OccupiesSlot()) ||
        Bookings.Count(b => b.OccupiesSlot()) < MaxPlayers;

    public bool IsPlayerReady(Guid userId) =>
        Bookings.Any(b => b.UserId == userId && !b.IsDeleted && b.Status == BookingStatus.CHECKED_IN) &&
        !Matches.Any(m => !m.IsDeleted && m.Status is MatchStatus.WAITING or MatchStatus.PLAYING &&
            m.Players.Any(p => p.UserId == userId));

    public bool CanStartRotation(DateTime now) => CanEditAttendance(now) && StartTime <= now &&
        AttendanceCompletedAt.HasValue && Bookings.Count(b => IsPlayerReady(b.UserId)) >= 4;

    public Result ChangeAttendance(Guid userId, BookingStatus status, Guid actorId, DateTime now)
    {
        if (userId == Guid.Empty || status is not (BookingStatus.CHECKED_IN or BookingStatus.NO_SHOW))
            return Result.Failure(Error.Validation("Attendance.InvalidRequest", "Chỉ chấp nhận CHECKED_IN hoặc NO_SHOW và UserId hợp lệ."));
        var allowed = ValidateAttendanceChange(actorId, now);
        if (allowed.IsFailure) return allowed;
        var booking = Bookings.FirstOrDefault(b => b.UserId == userId && !b.IsDeleted);
        if (booking is null)
            return Result.Failure(Error.NotFound("Attendance.BookingNotFound", "Người chơi chưa có đăng ký. Hãy sử dụng Thêm nhanh."));
        if (booking.Status is BookingStatus.WAITING or BookingStatus.ABSENT)
            return Result.Failure(Error.Conflict("Attendance.NotOfficial", "Người chơi chưa thuộc danh sách chính thức. Hãy sử dụng Thêm nhanh để kiểm tra sức chứa."));
        if (status == BookingStatus.CHECKED_IN)
        {
            if (!HasAttendanceRoom(booking))
                return Result.Failure(Error.Conflict("Attendance.CapacityReached", "Buổi chơi đã đủ sức chứa."));
            booking.MarkPresent(actorId, now);
        }
        else
        {
            if (Matches.Any(m => !m.IsDeleted && m.Status == MatchStatus.PLAYING && m.Players.Any(p => p.UserId == userId)))
                return Result.Failure(Error.Conflict("Attendance.PlayerPlaying", "Không thể đánh dấu vắng người đang thi đấu. Hãy kết thúc trận trước."));
            // Invalidate a pending match rather than leave an invalid three-player match.
            foreach (var match in Matches.Where(m => !m.IsDeleted && m.Status == MatchStatus.WAITING && m.Players.Any(p => p.UserId == userId)))
                match.SkipMatch();
            booking.MarkNoShow(actorId, now);
        }
        SynchronizeRecruitments(now);
        return Result.Success();
    }

    public Result<SessionBooking> AdmitWalkIn(Guid userId, Guid actorId, DateTime now)
    {
        if (userId == Guid.Empty)
            return Result.Failure<SessionBooking>(Error.Validation("Attendance.InvalidUser", "UserId không được để trống."));
        var allowed = ValidateAttendanceChange(actorId, now);
        if (allowed.IsFailure) return Result.Failure<SessionBooking>(allowed.Error);
        var booking = Bookings.FirstOrDefault(b => b.UserId == userId);
        if (!HasAttendanceRoom(booking))
            return Result.Failure<SessionBooking>(Error.Conflict("Attendance.CapacityReached", "Buổi chơi đã đủ sức chứa."));
        if (booking is null)
        {
            booking = new SessionBooking(Id, userId);
            Bookings.Add(booking);
        }
        booking.MarkPresent(actorId, now);
        SynchronizeRecruitments(now);
        return Result.Success(booking);
    }

    private Result ValidateAttendanceChange(Guid actorId, DateTime now)
    {
        if (actorId == Guid.Empty)
            return Result.Failure(Error.Validation("Attendance.InvalidActor", "Người điểm danh không hợp lệ."));
        return CanEditAttendance(now) ? Result.Success() : Result.Failure(Error.Conflict("Attendance.Closed",
            "Chỉ được điểm danh từ 1 giờ trước khi bắt đầu đến trước giờ kết thúc, khi buổi chưa chốt chi phí hoặc hoàn thành."));
    }

    public int RemainingRecruitmentSlots(GuestRecruitment recruitment)
    {
        // Reserve released slots for existing waiters; guests cannot bypass the RSVP queue.
        if (Bookings.Any(b => !b.IsDeleted && b.Status == BookingStatus.WAITING)) return 0;
        var sessionFree = MaxPlayers - Bookings.Count(b => b.OccupiesSlot());
        var recruitmentFree = recruitment.SlotsNeeded - Bookings.Count(b => b.RecruitmentId == recruitment.Id && b.OccupiesSlot());
        return Math.Max(0, Math.Min(sessionFree, recruitmentFree));
    }

    // Call while holding the session row lock, after changing bookings/capacity/status.
    public void SynchronizeRecruitments(DateTime now)
    {
        foreach (var recruitment in Recruitments.Where(r => !r.IsDeleted &&
                     r.Status is RecruitmentStatus.ACTIVE or RecruitmentStatus.FILLED))
        {
            if (IsDeleted || now >= EndTime || Status is SessionStatus.CALCULATED or SessionStatus.COMPLETED)
                recruitment.UpdateStatus(RecruitmentStatus.EXPIRED);
            else
                recruitment.UpdateStatus(Status == SessionStatus.OPEN && RemainingRecruitmentSlots(recruitment) > 0
                    ? RecruitmentStatus.ACTIVE : RecruitmentStatus.FILLED);
        }
    }

    public Result CompleteAttendance(Guid actorId, DateTime now)
    {
        var allowed = ValidateAttendanceChange(actorId, now);
        if (allowed.IsFailure) return allowed;
        if (AttendanceCompletedAt.HasValue) return Result.Success();
        AttendanceCompletedAt = now;
        AttendanceCompletedBy = actorId;
        return Result.Success();
    }

    public void UpdateStatus(SessionStatus status)
    {
        if (!CanTransitionTo(status))
            throw new InvalidOperationException("Invalid session status transition.");
        Status = status;
    }

    public Result ChangeOperationalStatus(SessionStatus target, bool confirmRemainingNoShows, Guid actorId, DateTime now)
    {
        if (!CanTransitionTo(target))
            return Result.Failure(Error.Conflict("Session.InvalidTransition", "Không thể chuyển trạng thái buổi chơi về bước trước sau khi đã bắt đầu hoặc chốt buổi."));
        if (target is SessionStatus.CALCULATED or SessionStatus.COMPLETED)
        {
            if (Matches.Any(m => !m.IsDeleted && m.Status == MatchStatus.PLAYING))
                return Result.Failure(Error.Conflict("Session.ActiveMatches", "Còn trận đang thi đấu. Hãy kết thúc các trận trước khi đóng buổi."));
            var notArrived = Bookings.Where(b => !b.IsDeleted && b.Status == BookingStatus.CONFIRMED).ToList();
            if (notArrived.Count > 0 && !confirmRemainingNoShows)
                return Result.Failure(Error.Conflict("Attendance.NoShowConfirmationRequired",
                    $"Còn {notArrived.Count} người đã đăng ký nhưng chưa có mặt. Hãy kiểm tra danh sách điểm danh và xác nhận đánh dấu NO_SHOW trước khi đóng buổi."));
            foreach (var booking in notArrived)
                booking.MarkNoShow(actorId, now);
        }
        UpdateStatus(target);
        SynchronizeRecruitments(now);
        return Result.Success();
    }

    public bool CanTransitionTo(SessionStatus target) => Enum.IsDefined(target) &&
        (Status == target || Status switch
        {
            SessionStatus.OPEN => target is SessionStatus.LOCKED or SessionStatus.PLAYING or SessionStatus.CALCULATED or SessionStatus.COMPLETED,
            SessionStatus.LOCKED => target is SessionStatus.OPEN or SessionStatus.PLAYING or SessionStatus.CALCULATED or SessionStatus.COMPLETED,
            SessionStatus.PLAYING => target is SessionStatus.CALCULATED or SessionStatus.COMPLETED,
            SessionStatus.CALCULATED => target == SessionStatus.COMPLETED,
            _ => false
        });

    public void UpdateStatus(string status)
    {
        UpdateStatus(ParseStatus(status));
    }

    private static SessionStatus ParseStatus(string status)
    {
        return Enum.TryParse<SessionStatus>(status, true, out var result) ? result : SessionStatus.OPEN;
    }
}
