using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.BadmintonClub.Domain.Services;
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
    public DateTime? RotationStartedAt { get; private set; }
    public Guid? RotationStartedBy { get; private set; }
    public long RotationVersion { get; private set; }
    public DateTime? NextProposalCheckAt { get; private set; }
    public int ShuttlecockUsed { get; private set; }
    public decimal CourtRentalCost { get; private set; }
    public decimal ExtraExpense { get; private set; }
    public decimal ShuttlecockPrice { get; private set; }
    public Guid? PublishedPreviewId { get; private set; }
    public string? PublishedRequestHash { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public Guid? PublishedBy { get; private set; }
    public Guid? PublishedFeeRuleId { get; private set; }
    public FeeMode? PublishedFeeMode { get; private set; }
    public DateTime? PublishedFeeRuleAppliedAt { get; private set; }
    public decimal PublishedDailyMemberFee { get; private set; }
    public decimal PublishedGuestFee { get; private set; }
    public decimal PublishedFemaleDiscountDaily { get; private set; }
    public decimal PublishedFemaleDiscountGuest { get; private set; }
    public int PublishedRoundRule { get; private set; }
    public decimal TotalCost { get; private set; }
    public decimal TotalReceivable { get; private set; }
    public decimal MonthlyFundCovered { get; private set; }
    public decimal TotalDiscount { get; private set; }
    public decimal PolicyDelta { get; private set; }
    public decimal RoundingDelta { get; private set; }
    public decimal AdjustmentDelta { get; private set; }
    public decimal BalanceDifference { get; private set; }

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
        if ((RotationStartedAt.HasValue || PublishedPreviewId.HasValue) && (StartTime != startTime || EndTime != endTime ||
            CourtCount != courtCount || MaxPlayers != maxPlayers || DeadlineBooking != deadlineBooking))
            throw new InvalidOperationException("Session operational parameters are frozen after rotation starts.");
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
        if (PublishedPreviewId.HasValue || Fees.Count > 0)
            throw new InvalidOperationException("Không thể thay đổi chi phí đã phát hành khoản thu.");
        if (shuttlecockUsed < 0 || !SettlementAllocationEngine.IsValidAmount(courtRentalCost) || !SettlementAllocationEngine.IsValidAmount(extraExpense))
            throw new ArgumentOutOfRangeException(nameof(shuttlecockUsed), "Chi phí không hợp lệ.");
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

    public bool IsPlayerReady(Guid userId) => IsPlayerReady(userId, DateTime.UtcNow);

    public bool IsPlayerReady(Guid userId, DateTime now) =>
        Bookings.Any(b => b.UserId == userId && Services.MatchRotationEngine.IsEligible(b)) &&
        MatchRotationEngine.HasRested(this, userId, now) &&
        !Matches.Any(m => !m.IsDeleted && m.Status is MatchStatus.WAITING or MatchStatus.PLAYING &&
            m.Players.Any(p => p.UserId == userId));

    public bool CanStartRotation(DateTime now) => !RotationStartedAt.HasValue && CanEditAttendance(now) && StartTime <= now &&
        AttendanceCompletedAt.HasValue && Bookings.Count(b => IsPlayerReady(b.UserId, now)) >= 4;

    public Result ChangeAttendance(Guid userId, BookingStatus status, Guid actorId, DateTime now, bool generateProposals = true)
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
        if (booking.Status == status) return Result.Success();
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
                match.SkipMatch(now);
            booking.MarkNoShow(actorId, now);
        }
        SynchronizeRecruitments(now);
        AttendanceChanged(now, generateProposals);
        return Result.Success();
    }

    public Result<SessionBooking> AdmitWalkIn(Guid userId, Guid actorId, DateTime now, bool generateProposals = true)
    {
        if (userId == Guid.Empty)
            return Result.Failure<SessionBooking>(Error.Validation("Attendance.InvalidUser", "UserId không được để trống."));
        var allowed = ValidateAttendanceChange(actorId, now);
        if (allowed.IsFailure) return Result.Failure<SessionBooking>(allowed.Error);
        var booking = Bookings.FirstOrDefault(b => b.UserId == userId);
        if (booking is { IsDeleted: false, Status: BookingStatus.CHECKED_IN }) return Result.Success(booking);
        if (!HasAttendanceRoom(booking))
            return Result.Failure<SessionBooking>(Error.Conflict("Attendance.CapacityReached", "Buổi chơi đã đủ sức chứa."));
        if (booking is null)
        {
            booking = new SessionBooking(Id, userId);
            Bookings.Add(booking);
        }
        booking.MarkPresent(actorId, now);
        SynchronizeRecruitments(now);
        AttendanceChanged(now, generateProposals);
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
        if (status is SessionStatus.CALCULATED or SessionStatus.COMPLETED && !PublishedPreviewId.HasValue)
            throw new InvalidOperationException("Hãy phê duyệt và phát hành khoản thu trước khi chốt buổi.");
        if (!CanTransitionTo(status))
            throw new InvalidOperationException("Invalid session status transition.");
        Status = status;
    }

    public Result ChangeOperationalStatus(SessionStatus target, bool confirmRemainingNoShows, Guid actorId, DateTime now)
    {
        if (RotationStartedAt.HasValue && target == SessionStatus.OPEN)
            return Result.Failure(Error.Conflict("Rotation.RegistrationLocked", "Không thể mở lại đăng ký sau khi bắt đầu điều phối."));
        if (RotationStartedAt.HasValue && target == SessionStatus.PLAYING && Status != SessionStatus.PLAYING)
            return Result.Failure(Error.Conflict("Rotation.StartMatchRequired", "Hãy bắt đầu trận đầu tiên để chuyển buổi sang đang chơi."));
        if (!CanTransitionTo(target))
            return Result.Failure(Error.Conflict("Session.InvalidTransition", "Không thể chuyển trạng thái buổi chơi về bước trước sau khi đã bắt đầu hoặc chốt buổi."));
        if (target is SessionStatus.CALCULATED or SessionStatus.COMPLETED && !PublishedPreviewId.HasValue)
            return Result.Failure(Error.Conflict("Settlement.ApprovalRequired", "Hãy phê duyệt và phát hành khoản thu trước khi chốt buổi."));
        if (target is SessionStatus.CALCULATED or SessionStatus.COMPLETED)
        {
            if (Matches.Any(m => !m.IsDeleted && m.Status == MatchStatus.PLAYING))
                return Result.Failure(Error.Conflict("Session.ActiveMatches", "Còn trận đang thi đấu. Hãy kết thúc các trận trước khi đóng buổi."));
            var notArrived = Bookings.Where(b => !b.IsDeleted && b.Status == BookingStatus.CONFIRMED).ToList();
            if (notArrived.Count > 0 && !confirmRemainingNoShows)
                return Result.Failure(Error.Conflict("Attendance.NoShowConfirmationRequired",
                    $"Còn {notArrived.Count} người đã đăng ký nhưng chưa có mặt. Hãy kiểm tra danh sách điểm danh và xác nhận đánh dấu NO_SHOW trước khi đóng buổi."));
        }
        ApplyOperationalStatus(target, actorId, now);
        return Result.Success();
    }

    private void ApplyOperationalStatus(SessionStatus target, Guid actorId, DateTime now)
    {
        if (target is SessionStatus.CALCULATED or SessionStatus.COMPLETED)
            foreach (var booking in Bookings.Where(b => !b.IsDeleted && b.Status == BookingStatus.CONFIRMED))
                booking.MarkNoShow(actorId, now);
        UpdateStatus(target);
        if (target is SessionStatus.CALCULATED or SessionStatus.COMPLETED) NextProposalCheckAt = null;
        if (target is SessionStatus.CALCULATED or SessionStatus.COMPLETED)
            foreach (var pending in Matches.Where(m => !m.IsDeleted && m.Status == MatchStatus.WAITING))
                pending.SkipMatch(now);
        SynchronizeRecruitments(now);
        if (RotationStartedAt.HasValue) RecordRotationChange(now);
    }

    public Result ValidateSettlement(DateTime now, bool confirmRemainingNoShows = false)
    {
        if (IsDeleted || PublishedPreviewId.HasValue || Fees.Count > 0 || Status is SessionStatus.CALCULATED or SessionStatus.COMPLETED)
            return Result.Failure(Error.Conflict("Settlement.AlreadyPublished", "Buổi chơi đã đóng hoặc đã có khoản thu, không thể phát hành lại."));
        if (Status is not (SessionStatus.OPEN or SessionStatus.LOCKED or SessionStatus.PLAYING) || !CanTransitionTo(SessionStatus.CALCULATED))
            return Result.Failure(Error.Conflict("Session.InvalidTransition", "Trạng thái buổi chơi không cho phép phát hành khoản thu."));
        if (now.Kind != DateTimeKind.Utc || StartTime.Kind != DateTimeKind.Utc || EndTime.Kind != DateTimeKind.Utc)
            return Result.Failure(Error.Validation("Settlement.UtcRequired", "Thời gian quyết toán phải sử dụng UTC."));
        if (now < EndTime)
            return Result.Failure(Error.Conflict("Settlement.NotEnded", "Buổi chơi chưa kết thúc khung giờ thuê sân."));
        if (Matches.Any(m => !m.IsDeleted && m.Status == MatchStatus.PLAYING))
            return Result.Failure(Error.Conflict("Session.ActiveMatches", "Còn trận đang thi đấu, hãy kết thúc trận trước khi chốt buổi."));
        if (!confirmRemainingNoShows && Bookings.Any(b => !b.IsDeleted && b.Status == BookingStatus.CONFIRMED))
            return Result.Failure(Error.Conflict("Attendance.NoShowConfirmationRequired", "Cần xác nhận người đã đăng ký nhưng chưa có mặt trước khi chốt buổi."));
        return Result.Success();
    }

    public Result PublishSettlement(GroupFeeRule rule, IReadOnlyCollection<AppUser> users,
        IReadOnlyCollection<GroupMember> memberships, SettlementCosts costs,
        IReadOnlyCollection<SettlementAdjustment> adjustments, Guid previewId, string requestHash,
        Guid actorId, DateTime now, bool confirmRemainingNoShows = false)
    {
        if (actorId == Guid.Empty || previewId == Guid.Empty || string.IsNullOrWhiteSpace(requestHash) ||
            requestHash.Length != 64 || requestHash.Any(c => !Uri.IsHexDigit(c)))
            return Result.Failure(Error.Validation("Settlement.InvalidPublication", "Thông tin người duyệt, bản xem trước hoặc mã xác nhận không hợp lệ."));
        if (PublishedPreviewId.HasValue)
            return PublishedPreviewId == previewId && PublishedRequestHash == requestHash && PublishedBy == actorId
                ? Result.Success()
                : Result.Failure(Error.Conflict("Settlement.AlreadyPublished", "Buổi chơi đã phát hành khoản thu từ một yêu cầu khác."));
        var calculated = SettlementAllocationEngine.Calculate(this, rule, users, memberships, costs, adjustments,
            now, confirmRemainingNoShows);
        if (calculated.IsFailure) return Result.Failure(calculated.Error);
        var allocation = calculated.Value;
        var paymentCodes = allocation.Members.Select(m => SessionFee.CreatePaymentCode(Id, m.UserId)).ToArray();
        if (paymentCodes.Distinct(StringComparer.Ordinal).Count() != paymentCodes.Length)
            return Result.Failure(Error.Conflict("Settlement.PaymentCodeConflict", "Mã thanh toán bị trùng, chưa phát hành khoản thu."));
        var fees = allocation.Members.Select(m => SessionFee.CreatePublished(Id, m, actorId, now)).ToArray();
        PublishedPreviewId = previewId;
        PublishedRequestHash = requestHash;
        PublishedAt = now;
        PublishedBy = actorId;
        PublishedFeeRuleId = allocation.Rule.Id;
        PublishedFeeMode = allocation.Rule.FeeMode;
        PublishedFeeRuleAppliedAt = allocation.Rule.AppliedAt;
        PublishedDailyMemberFee = allocation.Rule.DailyMemberFee;
        PublishedGuestFee = allocation.Rule.GuestFee;
        PublishedFemaleDiscountDaily = allocation.Rule.FemaleDiscountDaily;
        PublishedFemaleDiscountGuest = allocation.Rule.FemaleDiscountGuest;
        PublishedRoundRule = allocation.Rule.RoundRule;
        ShuttlecockUsed = allocation.Costs.ShuttlecockUsed;
        ShuttlecockPrice = allocation.Costs.ShuttlecockPrice;
        CourtRentalCost = allocation.Costs.CourtRentalCost;
        ExtraExpense = allocation.Costs.ExtraExpense;
        TotalCost = allocation.TotalCost;
        TotalReceivable = allocation.TotalReceivable;
        MonthlyFundCovered = allocation.MonthlyFundCovered;
        TotalDiscount = allocation.TotalDiscount;
        PolicyDelta = allocation.PolicyDelta;
        RoundingDelta = allocation.RoundingDelta;
        AdjustmentDelta = allocation.AdjustmentDelta;
        BalanceDifference = allocation.BalanceDifference;
        foreach (var fee in fees) Fees.Add(fee);
        ApplyOperationalStatus(SessionStatus.CALCULATED, actorId, now);
        RaiseDomainEvent(new SettlementPublishedDomainEvent(Id, GroupId, previewId, actorId, now,
            Array.AsReadOnly(fees.Select(f => f.Id).ToArray())));
        return Result.Success();
    }

    public bool CanTransitionTo(SessionStatus target) => Enum.IsDefined(target) &&
        (!RotationStartedAt.HasValue || target != SessionStatus.OPEN) &&
        (!RotationStartedAt.HasValue || target != SessionStatus.PLAYING || Status == SessionStatus.PLAYING) &&
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
    // --- UC13: Match rotation ---
    public Result InitializeRotation(Guid actorId, DateTime now)
    {
        if (RotationStartedAt.HasValue) return Result.Success();
        var allowed = ValidateRotation(now);
        if (allowed.IsFailure) return allowed;
        if (actorId == Guid.Empty || !AttendanceCompletedAt.HasValue)
            return Result.Failure(Error.Conflict("Rotation.AttendanceIncomplete", "Cần hoàn tất điểm danh trước khi điều phối."));
        if (MatchRotationEngine.Candidates(this, now).Count < 4 || !HasFreeCourt())
            return Result.Failure(Error.Conflict("Rotation.NotEnoughPlayers", "Cần sân trống và ít nhất 4 người sẵn sàng."));
        RotationStartedAt = now;
        RotationStartedBy = actorId;
        if (Status == SessionStatus.OPEN) Status = SessionStatus.LOCKED;
        ReconcileRotation(now);
        SynchronizeRecruitments(now);
        RecordRotationChange(now);
        return Result.Success();
    }

    public bool HasFreeCourt() => Enumerable.Range(1, Math.Max(0, CourtCount)).Any(c =>
        !Matches.Any(m => !m.IsDeleted && m.CourtNumber == c && m.Status is MatchStatus.WAITING or MatchStatus.PLAYING));

    public Result GenerateRotationProposals(DateTime now)
    {
        var allowed = ValidateRotation(now);
        if (allowed.IsFailure) return allowed;
        if (!RotationStartedAt.HasValue)
            return Result.Failure(Error.Conflict("Rotation.NotInitialized", "Chưa bắt đầu điều phối."));
        if (ReconcileRotation(now)) RecordRotationChange(now);
        return Result.Success();
    }

    public Result UpdateManualLineup(Guid matchId, IReadOnlyCollection<Guid>? teamA,
        IReadOnlyCollection<Guid>? teamB, long expectedRotationVersion, DateTime now)
    {
        if (teamA is null || teamB is null || teamA.Count != 2 || teamB.Count != 2 ||
            teamA.Concat(teamB).Any(id => id == Guid.Empty) || teamA.Concat(teamB).Distinct().Count() != 4 || expectedRotationVersion < 0)
            return Result.Failure(Error.Validation("Rotation.InvalidLineup", "Cần 4 người khác nhau, mỗi đội đúng 2 người và version hợp lệ."));
        var allowed = ValidateRotation(now);
        if (allowed.IsFailure) return allowed;
        if (!RotationStartedAt.HasValue)
            return Result.Failure(Error.Conflict("Rotation.NotInitialized", "Chưa bắt đầu điều phối."));
        if (expectedRotationVersion != RotationVersion)
            return Result.Failure(Error.Conflict("Rotation.VersionConflict", "Đội hình đã thay đổi. Hãy tải lại."));
        var match = Matches.SingleOrDefault(m => m.Id == matchId && m.SessionId == Id && !m.IsDeleted);
        if (match is null) return Result.Failure(Error.NotFound("Match.NotFound", "Không tìm thấy trận đấu."));
        bool Eligible(Guid id) => ManualPlayerIsEligible(id, now);
        var requested = teamA.Concat(teamB).ToHashSet();
        if (match.Status != MatchStatus.WAITING || match.TimeStart.HasValue || match.TimeEnd.HasValue || !ValidProposal(match, now) ||
            match.Players.Any(p => p.MatchId != match.Id || p.CheckinCourtAt.HasValue || !Eligible(p.UserId)) ||
            requested.Any(id => !Eligible(id)) ||
            Matches.Any(m => !m.IsDeleted && m.Id != match.Id && m.Status is MatchStatus.WAITING or MatchStatus.PLAYING &&
                (m.CourtNumber == match.CourtNumber || m.Players.Any(p => requested.Contains(p.UserId) || match.Players.Any(x => x.UserId == p.UserId)))))
            return Result.Failure(Error.Conflict("Rotation.StaleProposal", "Đội hình, sân hoặc người chơi không còn khả dụng. Hãy tải lại."));
        if (match.HasLineup(teamA, teamB)) return Result.Success();
        if (match.OriginalLineupSnapshot is null && match.Players.Any(p => p.IsManualSwapped))
            return Result.Failure(Error.Conflict("Rotation.MissingOriginalLineup", "Không xác định được gợi ý gốc của đội hình đã chỉnh."));
        match.OriginalLineupSnapshot?.Validate();
        match.ApplyManualLineup(teamA, teamB);
        UpdateRotationSchedule(now, true);
        RecordRotationChange(now);
        return Result.Success();
    }

    public Result SetRotationPause(Guid userId, bool isPaused, long expectedRotationVersion,
        Guid? replacementUserId, DateTime now, bool generateProposals = true)
        => SetRotationPauseCore(userId, isPaused, expectedRotationVersion, replacementUserId, now,
            generateProposals, RotationPauseSource.Host);

    public Result SetSelfRotationPause(Guid userId, bool isPaused, long expectedRotationVersion,
        DateTime now, bool generateProposals = true)
    {
        if (userId == Guid.Empty || expectedRotationVersion < 0)
            return Result.Failure(Error.Validation("Rotation.InvalidPause", "Người chơi/version không hợp lệ."));
        ValidateRotationPauseMetadata();
        var allowed = ValidateRotation(now);
        if (allowed.IsFailure) return allowed;
        if (!RotationStartedAt.HasValue)
            return Result.Failure(Error.Conflict("Rotation.NotInitialized", "Chưa bắt đầu điều phối."));
        if (expectedRotationVersion != RotationVersion)
            return Result.Failure(Error.Conflict("Rotation.VersionConflict", "Điều phối đã thay đổi. Hãy tải lại."));
        var booking = Bookings.SingleOrDefault(b => b.SessionId == Id && b.UserId == userId && !b.IsDeleted);
        if (booking is null || !MatchRotationEngine.IsCheckedInParticipant(booking) || booking.User is null ||
            Matches.Any(m => !m.IsDeleted && m.Status == MatchStatus.PLAYING && m.Players.Any(p => p.UserId == userId)))
            return Result.Failure(Error.Conflict("Rotation.CannotPause", "Chỉ thay đổi nghỉ khi đã điểm danh và chưa thi đấu."));
        if (booking.IsRotationPaused && booking.RotationPauseSource != RotationPauseSource.Self)
            return Result.Failure(Error.Conflict("Rotation.HostPauseRequiresHost", "Chỉ Host được bỏ trạng thái nghỉ này."));
        return SetRotationPauseCore(userId, isPaused, expectedRotationVersion, null, now,
            generateProposals, RotationPauseSource.Self);
    }

    public void ValidateRotationPauseMetadata()
    {
        foreach (var booking in Bookings)
            if (!booking.HasValidPauseMetadata() || booking.SelfPauseTargetMatchId is Guid target &&
                !Matches.Any(m => m.Id == target && m.SessionId == Id && !m.IsDeleted && m.Status == MatchStatus.PLAYING))
                throw new InvalidRotationPauseMetadataException(Id, booking.UserId);
    }

    private Result SetRotationPauseCore(Guid userId, bool isPaused, long expectedRotationVersion,
        Guid? replacementUserId, DateTime now, bool generateProposals, RotationPauseSource source)
    {
        ValidateRotationPauseMetadata();
        if (userId == Guid.Empty || expectedRotationVersion < 0 || replacementUserId == Guid.Empty ||
            replacementUserId == userId || (!isPaused && replacementUserId.HasValue))
            return Result.Failure(Error.Validation("Rotation.InvalidPause", "Thông tin tạm nghỉ/người thay không hợp lệ."));
        if (IsDeleted || (isPaused && ValidateRotation(now).IsFailure))
            return Result.Failure(Error.Conflict("Rotation.Closed", "Không được đặt tạm nghỉ khi buổi chưa bắt đầu hoặc đã đóng."));
        if (!RotationStartedAt.HasValue)
            return Result.Failure(Error.Conflict("Rotation.NotInitialized", "Chưa bắt đầu điều phối."));
        if (expectedRotationVersion != RotationVersion)
            return Result.Failure(Error.Conflict("Rotation.VersionConflict", "Điều phối đã thay đổi. Hãy tải lại."));
        var booking = Bookings.SingleOrDefault(b => b.SessionId == Id && b.UserId == userId && !b.IsDeleted);
        if (booking is null) return Result.Failure(Error.NotFound("Attendance.BookingNotFound", "Không tìm thấy người chơi trong buổi."));
        if (isPaused && (!MatchRotationEngine.IsCheckedInParticipant(booking) || booking.User is null ||
            Matches.Any(m => !m.IsDeleted && m.Status == MatchStatus.PLAYING && m.Players.Any(p => p.UserId == userId))))
            return Result.Failure(Error.Conflict("Rotation.CannotPause", "Chỉ tạm nghỉ người đã điểm danh, có tài khoản hợp lệ và chưa thi đấu."));
        var pending = Matches.Where(m => !m.IsDeleted && m.Status == MatchStatus.WAITING && m.Players.Any(p => p.UserId == userId)).ToList();
        if (pending.Count > 1 || (replacementUserId.HasValue && pending.Count != 1))
            return Result.Failure(Error.Conflict("Rotation.InvalidReplacement", "Người thay chỉ áp dụng cho một đề xuất đang giữ người này."));
        var samePauseOwner = source == RotationPauseSource.Self
            ? booking.RotationPauseSource == RotationPauseSource.Self
            : booking.RotationPauseSource != RotationPauseSource.Self;
        if (booking.IsRotationPaused == isPaused && replacementUserId is null && (!isPaused || samePauseOwner))
            return Result.Success();
        var version = RotationVersion;
        if (isPaused && replacementUserId is Guid replacement)
        {
            var match = pending.Single();
            if (!ManualPlayerIsEligible(replacement, now) || match.Players.Any(p => p.UserId == replacement) ||
                Matches.Any(m => !m.IsDeleted && m.Id != match.Id && m.Status is MatchStatus.WAITING or MatchStatus.PLAYING && m.Players.Any(p => p.UserId == replacement)))
                return Result.Failure(Error.Conflict("Rotation.InvalidReplacement", "Người thay không còn khả dụng."));
            var teamA = match.Players.Where(p => p.SideTeam == SideTeam.TEAM_A).Select(p => p.UserId == userId ? replacement : p.UserId).ToArray();
            var teamB = match.Players.Where(p => p.SideTeam == SideTeam.TEAM_B).Select(p => p.UserId == userId ? replacement : p.UserId).ToArray();
            var change = UpdateManualLineup(match.Id, teamA, teamB, expectedRotationVersion, now);
            if (change.IsFailure) return change;
        }
        else if (isPaused)
            foreach (var match in pending) match.SkipMatch(now);
        booking.SetRotationPaused(isPaused, source);
        // Resume never fakes an end time/check-in or bypasses the existing rest period.
        if (ValidateRotation(now).IsSuccess) ReconcileRotation(now, generateProposals);
        else NextProposalCheckAt = null;
        if (RotationVersion == version) RecordRotationChange(now);
        return Result.Success();
    }

    public Result RestoreManualLineup(Guid matchId, long expectedRotationVersion, DateTime now)
    {
        if (expectedRotationVersion < 0)
            return Result.Failure(Error.Validation("Rotation.InvalidVersion", "Version không được âm."));
        var allowed = ValidateRotation(now);
        if (allowed.IsFailure) return allowed;
        if (!RotationStartedAt.HasValue)
            return Result.Failure(Error.Conflict("Rotation.NotInitialized", "Chưa bắt đầu điều phối."));
        if (expectedRotationVersion != RotationVersion)
            return Result.Failure(Error.Conflict("Rotation.VersionConflict", "Đội hình đã thay đổi. Hãy tải lại."));
        var match = Matches.SingleOrDefault(m => m.Id == matchId && m.SessionId == Id && !m.IsDeleted);
        if (match is null) return Result.Failure(Error.NotFound("Match.NotFound", "Không tìm thấy trận đấu."));
        if (match.Status != MatchStatus.WAITING || match.TimeStart.HasValue || match.TimeEnd.HasValue ||
            match.CourtNumber < 1 || match.CourtNumber > CourtCount || match.OriginalLineupSnapshot is null)
            return Result.Failure(Error.Conflict("Rotation.CannotRestore", "Chỉ khôi phục trận đề xuất có gợi ý gốc hợp lệ."));
        var original = match.OriginalLineupSnapshot;
        original.Validate();
        var ids = original.TeamA.Concat(original.TeamB).ToHashSet();
        if (ids.Any(id => !ManualPlayerIsEligible(id, now)) ||
            Matches.Any(m => !m.IsDeleted && m.Id != match.Id && m.Status is MatchStatus.WAITING or MatchStatus.PLAYING &&
                (m.CourtNumber == match.CourtNumber || m.Players.Any(p => ids.Contains(p.UserId)))))
            return Result.Failure(Error.Conflict("Rotation.OriginalPlayersUnavailable", "Người trong gợi ý gốc không còn khả dụng. Không thể khôi phục."));
        if (match.HasLineup(original.TeamA, original.TeamB)) return Result.Success();
        match.ApplyManualLineup(original.TeamA, original.TeamB);
        UpdateRotationSchedule(now, true);
        RecordRotationChange(now);
        return Result.Success();
    }

    private bool ManualPlayerIsEligible(Guid userId, DateTime now) => Bookings.Any(b => b.SessionId == Id && b.UserId == userId &&
        !b.IsDeleted && b.Status == BookingStatus.CHECKED_IN && !b.IsRotationPaused &&
        b.User is { IsActive: true, IsDeleted: false } && b.User.Id == userId) && MatchRotationEngine.HasRested(this, userId, now);

    public Result StartRotationMatch(Guid matchId, DateTime now) => StartRotationMatchCore(matchId, now, null);

    public Result ConfirmManualMatch(Guid matchId, long expectedRotationVersion, DateTime now)
    {
        if (expectedRotationVersion < 0)
            return Result.Failure(Error.Validation("Rotation.InvalidVersion", "Version không được âm."));
        return StartRotationMatchCore(matchId, now, expectedRotationVersion);
    }

    private Result StartRotationMatchCore(Guid matchId, DateTime now, long? expectedRotationVersion)
    {
        var match = Matches.FirstOrDefault(m => m.Id == matchId && m.SessionId == Id && !m.IsDeleted);
        if (match is null) return Result.Failure(Error.NotFound("Match.NotFound", "Không tìm thấy trận đấu."));
        if (expectedRotationVersion is null && match.OriginalLineupSnapshot is not null)
            return Result.Failure(Error.Conflict("Rotation.ManualConfirmationRequired", "Đội hình đã qua can thiệp thủ công. Cần xác nhận kèm version."));
        if (match.Status == MatchStatus.PLAYING) return Result.Success();
        if (expectedRotationVersion.HasValue && expectedRotationVersion != RotationVersion)
            return Result.Failure(Error.Conflict("Rotation.VersionConflict", "Đội hình đã thay đổi. Hãy tải lại trước khi xác nhận."));
        var allowed = ValidateRotation(now);
        if (allowed.IsFailure) return allowed;
        if (!RotationStartedAt.HasValue || match.Status != MatchStatus.WAITING || !ValidProposal(match, now) ||
            (expectedRotationVersion.HasValue && (match.TimeStart.HasValue || match.TimeEnd.HasValue ||
                match.Players.Any(p => p.MatchId != match.Id || p.CheckinCourtAt.HasValue || !ManualPlayerIsEligible(p.UserId, now)))) ||
            Matches.Any(m => !m.IsDeleted && m.Id != match.Id && m.Status is MatchStatus.WAITING or MatchStatus.PLAYING &&
                (m.CourtNumber == match.CourtNumber || m.Players.Any(p => match.Players.Any(x => x.UserId == p.UserId)))))
            return Result.Failure(Error.Conflict("Rotation.StaleProposal", "Đội hình hoặc sân không còn hợp lệ. Hãy tải lại."));
        match.StartMatch(now);
        foreach (var booking in Bookings.Where(b => !b.IsDeleted && b.IsRotationPaused &&
            b.RotationPauseSource == RotationPauseSource.Self && b.SelfPauseTargetMatchId is null))
            booking.BindSelfPauseTarget(match.Id);
        Status = SessionStatus.PLAYING;
        UpdateRotationSchedule(now, true);
        RecordRotationChange(now, match);
        return Result.Success();
    }

    public Result FinishRotationMatch(Guid matchId, DateTime now, bool generateProposals = true)
    {
        var match = Matches.FirstOrDefault(m => m.Id == matchId && !m.IsDeleted);
        if (match is null) return Result.Failure(Error.NotFound("Match.NotFound", "Không tìm thấy trận đấu."));
        if (match.Status == MatchStatus.FINISHED) return Result.Success();
        if (IsDeleted || Status is SessionStatus.CALCULATED or SessionStatus.COMPLETED ||
            match.Status != MatchStatus.PLAYING || !match.TimeStart.HasValue || now < match.TimeStart.Value)
            return Result.Failure(Error.Conflict("Match.NotPlaying", "Chỉ được kết thúc trận đang thi đấu."));
        ValidateRotationPauseMetadata();
        match.EndMatch(now);
        foreach (var booking in Bookings.Where(b => b.IsRotationPaused &&
            b.RotationPauseSource == RotationPauseSource.Self && b.SelfPauseTargetMatchId == match.Id))
            booking.SetRotationPaused(false, RotationPauseSource.Self);
        ReconcileRotation(now, generateProposals);
        RecordRotationChange(now);
        return Result.Success();
    }

    public Result PrepareRotationDeletion(DateTime now, string? deletedBy = null)
    {
        if (Matches.Any(m => !m.IsDeleted && m.Status == MatchStatus.PLAYING))
            return Result.Failure(Error.Conflict("Session.ActiveMatches", "Hãy kết thúc các trận trước khi xóa buổi."));
        foreach (var match in Matches.Where(m => !m.IsDeleted && m.Status == MatchStatus.WAITING)) match.SkipMatch(now);
        // Mark only the session: DbSet.Remove would cascade-delete non-soft-deletable MatchPlayer history.
        IsDeleted = true;
        NextProposalCheckAt = null;
        DeletedOnUtc = new DateTimeOffset(now);
        DeletedBy = deletedBy;
        if (RotationStartedAt.HasValue) RecordRotationChange(now);
        return Result.Success();
    }

    private Result ValidateRotation(DateTime now)
    {
        ValidateRotationPauseMetadata();
        return !IsDeleted && StartTime <= now && now < EndTime &&
            Status is SessionStatus.OPEN or SessionStatus.LOCKED or SessionStatus.PLAYING
            ? Result.Success() : Result.Failure(Error.Conflict("Rotation.Closed", "Chỉ điều phối trong thời gian diễn ra buổi chưa đóng."));
    }

    private bool ValidProposal(SessionMatch match, DateTime now) => match.CourtNumber >= 1 && match.CourtNumber <= CourtCount &&
        match.Players.Count == 4 && match.Players.Select(p => p.UserId).Distinct().Count() == 4 &&
        match.Players.Count(p => p.SideTeam == SideTeam.TEAM_A) == 2 && match.Players.Count(p => p.SideTeam == SideTeam.TEAM_B) == 2 &&
        match.Players.All(p => Bookings.Any(b => b.UserId == p.UserId && MatchRotationEngine.IsEligible(b)) &&
            MatchRotationEngine.HasRested(this, p.UserId, now));

    private bool ReconcileRotation(DateTime now, bool generateProposals = true)
    {
        var changed = false;
        foreach (var match in Matches.Where(m => !m.IsDeleted && m.Status == MatchStatus.WAITING).ToList())
            if (!ValidProposal(match, now)) { match.SkipMatch(now); changed = true; }
        if (!generateProposals || !RotationStartedAt.HasValue || ValidateRotation(now).IsFailure)
        {
            NextProposalCheckAt = null;
            return changed;
        }
        foreach (var court in Enumerable.Range(1, CourtCount))
        {
            if (Matches.Any(m => !m.IsDeleted && m.CourtNumber == court && m.Status is MatchStatus.WAITING or MatchStatus.PLAYING)) continue;
            var selected = MatchRotationEngine.Candidates(this, now).Take(4).ToList();
            if (selected.Count < 4) break;
            var round = Matches.Where(m => m.CourtNumber == court).Select(m => m.RoundNumber).DefaultIfEmpty(0).Max() + 1;
            Matches.Add(SessionMatch.Propose(Id, court, round, MatchRotationEngine.Balance(selected)));
            changed = true;
        }
        UpdateRotationSchedule(now, generateProposals);
        return changed;
    }

    private void UpdateRotationSchedule(DateTime now, bool enabled)
    {
        NextProposalCheckAt = enabled && RotationStartedAt.HasValue && ValidateRotation(now).IsSuccess && HasFreeCourt()
            ? MatchRotationEngine.NextCandidateTime(this, now) : null;
        if (NextProposalCheckAt >= EndTime) NextProposalCheckAt = null;
    }

    public void ProcessScheduledRotation(DateTime now, bool enabled, bool recover = false)
    {
        if (!recover && (NextProposalCheckAt is null || NextProposalCheckAt > now)) return;
        if (!RotationStartedAt.HasValue) { NextProposalCheckAt = null; return; }
        if (ReconcileRotation(now, enabled)) RecordRotationChange(now);
    }

    private void AttendanceChanged(DateTime now, bool generateProposals)
    {
        if (!RotationStartedAt.HasValue) return;
        ReconcileRotation(now, generateProposals);
        RecordRotationChange(now);
    }

    private void RecordRotationChange(DateTime now, SessionMatch? startedMatch = null)
    {
        RotationVersion++;
        RaiseDomainEvent(new RotationChangedDomainEvent(Id, RotationVersion, now, startedMatch?.Id,
            startedMatch?.CourtNumber, startedMatch?.Players.Select(p => p.UserId).ToList() ?? []));
    }
}
