using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public partial class BadmintonSession
{
    public bool AdvanceEnabled { get; private set; }
    public decimal? AdvanceAmount { get; private set; }
    public virtual ICollection<SessionAdvancePayment> AdvancePayments { get; private set; } = new List<SessionAdvancePayment>();

    public Result ConfigureAdvance(bool enabled, decimal? amount, GroupFeeRule? rule)
    {
        if (!enabled && AdvancePayments.Any()) return Result.Failure(Error.Conflict("Advance.ConfigInUse", "Buổi đã có khoản ứng. Hãy giữ cấu hình đến khi chốt buổi."));
        if (enabled && (rule is null || rule.FeeMode == FeeMode.DYNAMIC && (!amount.HasValue || !ValidAdvanceAmount(amount.Value))))
            return Result.Failure(Error.Validation("Advance.InvalidAmount", "Cần biểu phí và số tiền ứng hợp lệ cho chế độ chia theo thực tế."));
        AdvanceEnabled = enabled;
        AdvanceAmount = rule?.FeeMode == FeeMode.DYNAMIC && enabled ? amount : null;
        return Result.Success();
    }

    public bool CanAdvance(Guid user, DateTime now) => AdvanceEnabled && !IsDeleted && StartTime > now &&
        Status is SessionStatus.OPEN or SessionStatus.LOCKED && HasActiveAdvanceBooking(user);

    public bool HasActiveAdvanceBooking(Guid user) => Bookings.Any(b =>
        b.UserId == user && !b.IsDeleted && b.RecruitmentId == null &&
        b.Status is BookingStatus.CONFIRMED or BookingStatus.CHECKED_IN);

    public decimal QuoteAdvance(GroupFeeRule rule, GroupMember member, short gender)
    {
        if (member.MemberType == "MONTHLY" && member.MonthlyExpiryDate >= StartTime && rule.FeeMode == FeeMode.FIXED)
            return 0m;
        if (rule.FeeMode == FeeMode.DYNAMIC) return AdvanceAmount.GetValueOrDefault();
        var net = Math.Max(0, rule.DailyMemberFee - (gender == (short)Gender.Female ? rule.FemaleDiscountDaily : 0));
        return rule.RoundRule > 0 ? decimal.Ceiling(net / rule.RoundRule) * rule.RoundRule : 0m;
    }

    public static bool ValidAdvanceAmount(decimal amount) =>
        LedgerBalance.IsValidAmount(amount) && amount <= 9999999999999m && amount == decimal.Truncate(amount);

    public void CloseAdvances(string reason, DateTime now, Guid? userId = null)
    {
        foreach (var advance in AdvancePayments.Where(a => !a.IsDeleted && (!userId.HasValue || a.UserId == userId)))
            advance.Close(reason, now);
    }
}
