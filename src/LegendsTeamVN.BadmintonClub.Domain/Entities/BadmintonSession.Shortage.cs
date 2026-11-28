using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public partial class BadmintonSession
{
    public int? MinimumPlayers { get; private set; }
    public int? ShortageCheckMinutes { get; private set; }
    public DateTime? ShortageReminderAt { get; private set; }
    public bool ShortageReminderSent { get; private set; }
    public bool ShortageInitialChecked { get; private set; }
    public bool ShortageFinalChecked { get; private set; }
    public DateTime? NextShortageCheckAt { get; private set; }
    public DateTime? NotHeldAt { get; private set; }

    private DateTime FinalShortageCheckAt => StartTime.AddMinutes(-30);
    private DateTime InitialShortageCheckAt => StartTime.AddMinutes(-Math.Min(
        ShortageCheckMinutes.GetValueOrDefault(), StartTime.Ticks / TimeSpan.TicksPerMinute));
    public int ConfirmedPlayerCount() =>
        Bookings.Count(b => !b.IsDeleted && b.Status is BookingStatus.CONFIRMED or BookingStatus.CHECKED_IN);

    public Result ConfigureShortage(int? minimumPlayers, int? checkMinutes, int capacity, bool startChanged)
    {
        var minimum = minimumPlayers ?? MinimumPlayers ?? (checkMinutes.HasValue ? 4 : (int?)null);
        var minutes = checkMinutes ?? ShortageCheckMinutes ?? (minimum.HasValue ? 120 : (int?)null);
        if (minimum is null && minutes is null) return Result.Success();
        if (minimum is null || minimum < 4 || minimum > capacity || minutes is null or < 1)
            return Result.Failure(Error.Validation("Session.InvalidShortageSettings",
                "Số người tối thiểu phải từ 4 đến sức chứa và mốc kiểm tra phải trước giờ chơi."));
        MinimumPlayers = minimum;
        ShortageCheckMinutes = minutes;
        if (startChanged) ShortageReminderAt = null;
        return Result.Success();
    }

    public Result SetShortageReminder(int? delayMinutes, DateTime now)
    {
        if (MinimumPlayers is null || IsDeleted || Status is not (SessionStatus.OPEN or SessionStatus.LOCKED) ||
            now >= FinalShortageCheckAt || ShortageReminderSent)
            return Result.Failure(Error.Conflict("Session.ReminderUnavailable", "Không thể đổi lịch nhắc thêm cho buổi này."));
        if (delayMinutes is not (null or 15 or 30 or 60))
            return Result.Failure(Error.Validation("Session.InvalidReminderDelay", "Chọn nhắc sau 15, 30 hoặc 60 phút."));
        var reminder = delayMinutes is int delay ? now.AddMinutes(delay) : (DateTime?)null;
        if (reminder >= FinalShortageCheckAt)
            return Result.Failure(Error.Validation("Session.ReminderTooLate", "Giờ nhắc thêm phải trước mốc nhắc cuối."));
        ShortageReminderAt = reminder;
        ScheduleShortageCheck();
        return Result.Success();
    }

    public void ScheduleShortageCheck()
    {
        if (MinimumPlayers is null || IsDeleted || Status is not (SessionStatus.OPEN or SessionStatus.LOCKED))
        {
            NextShortageCheckAt = null;
            return;
        }
        var next = ShortageFinalChecked ? EndTime : FinalShortageCheckAt;
        if (!ShortageInitialChecked && !ShortageFinalChecked && InitialShortageCheckAt < next)
            next = InitialShortageCheckAt;
        if (ShortageReminderAt is DateTime reminder && reminder < next) next = reminder;
        NextShortageCheckAt = next;
    }

    public SessionShortageNotice? ProcessShortage(DateTime now, bool hasStoredFees = false)
    {
        if (NextShortageCheckAt is not DateTime due || due > now || IsDeleted ||
            Status is not (SessionStatus.OPEN or SessionStatus.LOCKED)) return null;
        if (PublishedPreviewId.HasValue || hasStoredFees || Fees.Any(f => !f.IsDeleted) || Matches.Any(m => m.TimeStart.HasValue))
        {
            NextShortageCheckAt = null;
            return null;
        }
        if (now >= EndTime)
        {
            Status = SessionStatus.NOT_HELD;
            NotHeldAt = now;
            NextProposalCheckAt = null;
            ShortageReminderAt = null;
            foreach (var match in Matches.Where(m => !m.IsDeleted && m.Status == MatchStatus.WAITING))
                match.SkipMatch(now);
            SynchronizeRecruitments(now);
            if (RotationStartedAt.HasValue) RecordRotationChange(now);
            ScheduleShortageCheck();
            return SessionShortageNotice.NotHeld;
        }

        SessionShortageNotice? notice = null;
        if (now >= FinalShortageCheckAt && !ShortageFinalChecked)
        {
            ShortageInitialChecked = ShortageFinalChecked = true;
            ShortageReminderAt = null;
            notice = SessionShortageNotice.Final;
        }
        else if (ShortageReminderAt is DateTime reminder && reminder <= now)
        {
            ShortageReminderAt = null;
            ShortageReminderSent = ConfirmedPlayerCount() < MinimumPlayers;
            if (ShortageReminderSent) notice = SessionShortageNotice.Reminder;
            if (now >= InitialShortageCheckAt) ShortageInitialChecked = true;
        }
        else if (!ShortageInitialChecked && now >= InitialShortageCheckAt)
        {
            ShortageInitialChecked = true;
            notice = SessionShortageNotice.Initial;
        }
        ScheduleShortageCheck();
        return now < StartTime && ConfirmedPlayerCount() < MinimumPlayers ? notice : null;
    }
}
