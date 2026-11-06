using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Domain.Aggregates;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class MonthlyContribution : AggregateRoot<Guid>
{
    public Guid GroupId { get; private set; }
    public Guid UserId { get; private set; }
    public DateOnly ContributionMonth { get; private set; }
    public decimal MonthlyFee { get; private set; }
    public decimal FemaleDiscountApplied { get; private set; }
    public decimal AmountDue { get; private set; }
    public MonthlyContributionStatus Status { get; private set; }
    public DateTime IssuedAt { get; private set; }
    public Guid IssuedBy { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public Guid? CollectedBy { get; private set; }

    protected MonthlyContribution() { }

    public static DateOnly CurrentMonth(DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Thời điểm phải sử dụng UTC.", nameof(utcNow));
        var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"));
        return new DateOnly(local.Year, local.Month, 1);
    }

    public static DateTime PeriodEndUtc(DateOnly month)
    {
        if (month.Day != 1 || month.Year == 9999 && month.Month == 12)
            throw new ArgumentOutOfRangeException(nameof(month), "Kỳ phải là ngày đầu tháng hợp lệ.");
        var next = month.AddMonths(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(next, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh")).AddTicks(-10);
    }

    public static Result CheckEligibility(GroupMember member, DateOnly month, DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc || month.Day != 1 || month != CurrentMonth(utcNow))
            return Unsupported("Chỉ hỗ trợ kỳ hiện tại theo giờ Việt Nam.");
        if (member.IsDeleted || member.Status != ClubMemberStatus.ACTIVE || member.MemberType != "MONTHLY")
            return Unsupported("Chỉ hỗ trợ hội viên tháng đang hoạt động.");
        var start = TimeZoneInfo.ConvertTimeToUtc(month.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
            TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"));
        if (member.JoinedAt.Kind != DateTimeKind.Utc || member.JoinedAt > start || !member.MonthlyMembershipSince.HasValue ||
            member.MonthlyMembershipSince.Value.Kind != DateTimeKind.Utc || member.MonthlyMembershipSince > start)
            return Unsupported("Chưa đủ dữ liệu xác nhận tư cách hội viên tháng trong toàn kỳ.");
        return Result.Success();
    }

    public static Result<MonthlyContribution> Issue(GroupMember member, GroupFeeRule rule, short gender,
        DateOnly month, Guid actor, DateTime utcNow)
    {
        var eligibility = CheckEligibility(member, month, utcNow);
        if (eligibility.IsFailure) return Result.Failure<MonthlyContribution>(eligibility.Error);
        if (actor == Guid.Empty || member.GroupId != rule.GroupId || member.GroupId == Guid.Empty || member.UserId == Guid.Empty)
            return Result.Failure<MonthlyContribution>(Error.Validation("Ledger.InvalidSource", "Nguồn nghĩa vụ tháng không hợp lệ."));
        var quote = rule.CalculateMonthlyContribution(gender);
        if (quote.IsFailure) return Result.Failure<MonthlyContribution>(quote.Error);
        return Result.Success(new MonthlyContribution
        {
            Id = Guid.NewGuid(), GroupId = member.GroupId, UserId = member.UserId,
            ContributionMonth = month, MonthlyFee = rule.MonthlyFee,
            FemaleDiscountApplied = gender == (short)Gender.Female ? rule.FemaleDiscountMonthly : 0m,
            AmountDue = quote.Value, Status = MonthlyContributionStatus.UNPAID,
            IssuedAt = utcNow, IssuedBy = actor
        });
    }

    public decimal GetOutstandingAmount() => Status == MonthlyContributionStatus.UNPAID ? AmountDue : 0m;

    public Result ConfirmCollection(GroupMember member, decimal expectedAmount, Guid actor, DateTime utcNow)
    {
        if (Status == MonthlyContributionStatus.PAID)
            return Result.Failure(Error.Conflict("Ledger.MonthAlreadyCollected", "Nghĩa vụ tháng đã được thanh toán."));
        if (IsDeleted || member.GroupId != GroupId || member.UserId != UserId || actor == Guid.Empty)
            return Result.Failure(Error.Validation("Ledger.InvalidSource", "Nguồn thu tháng không hợp lệ."));
        var eligible = CheckEligibility(member, ContributionMonth, utcNow);
        if (eligible.IsFailure) return eligible;
        if (expectedAmount != AmountDue)
            return Result.Failure(Error.Conflict("Ledger.ContributionAmountMismatch", "Số tiền không khớp nghĩa vụ đã phát hành."));
        var renewal = member.RenewMonthlyMembership(ContributionMonth);
        if (renewal.IsFailure) return renewal;
        Status = MonthlyContributionStatus.PAID;
        PaidAt = utcNow;
        CollectedBy = actor;
        return Result.Success();
    }

    private static Result Unsupported(string message) =>
        Result.Failure(Error.Conflict("Ledger.MonthlyPolicyUnsupported", message));
}
