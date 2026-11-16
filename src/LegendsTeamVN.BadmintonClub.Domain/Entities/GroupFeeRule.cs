using LegendsTeamVN.Core.Utilities.Results;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Domain.Entities;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class GroupFeeRule : Entity<Guid>
{
    public Guid GroupId { get; private set; }

    /// <summary>
    /// Chế độ tính phí của quy tắc chia tiền. Miền giá trị: DYNAMIC, FIXED.
    /// </summary>
    public FeeMode FeeMode { get; private set; } = FeeMode.DYNAMIC;

    public decimal MonthlyFee { get; private set; }
    public decimal DailyMemberFee { get; private set; }
    public decimal GuestFee { get; private set; }
    public decimal FemaleDiscountMonthly { get; private set; }
    public decimal FemaleDiscountDaily { get; private set; }
    public decimal FemaleDiscountGuest { get; private set; }
    public int RoundRule { get; private set; }
    public DateTime AppliedAt { get; private set; }

    public virtual ClubGroup Group { get; private set; } = default!;

    protected GroupFeeRule() { }

    public GroupFeeRule(
        Guid groupId,
        FeeMode feeMode = FeeMode.DYNAMIC,
        decimal monthlyFee = 0,
        decimal dailyMemberFee = 0,
        decimal guestFee = 0,
        decimal femaleDiscountMonthly = 0,
        decimal femaleDiscountDaily = 0,
        decimal femaleDiscountGuest = 0,
        int roundRule = 1000)
    {
        Id = Guid.NewGuid();
        GroupId = groupId;
        FeeMode = feeMode;
        MonthlyFee = monthlyFee;
        DailyMemberFee = dailyMemberFee;
        GuestFee = guestFee;
        FemaleDiscountMonthly = femaleDiscountMonthly;
        FemaleDiscountDaily = femaleDiscountDaily;
        FemaleDiscountGuest = femaleDiscountGuest;
        RoundRule = roundRule;
        AppliedAt = DateTime.UtcNow;
    }

    public GroupFeeRule(
        Guid groupId,
        string feeMode,
        decimal monthlyFee = 0,
        decimal dailyMemberFee = 0,
        decimal guestFee = 0,
        decimal femaleDiscountMonthly = 0,
        decimal femaleDiscountDaily = 0,
        decimal femaleDiscountGuest = 0,
        int roundRule = 1000)
        : this(groupId, ParseFeeMode(feeMode), monthlyFee, dailyMemberFee, guestFee, femaleDiscountMonthly, femaleDiscountDaily, femaleDiscountGuest, roundRule)
    {
    }

    public void UpdateFees(
        FeeMode feeMode,
        decimal monthlyFee,
        decimal dailyMemberFee,
        decimal guestFee,
        decimal femaleDiscountMonthly,
        decimal femaleDiscountDaily,
        decimal femaleDiscountGuest,
        int roundRule)
    {
        FeeMode = feeMode;
        MonthlyFee = monthlyFee;
        DailyMemberFee = dailyMemberFee;
        GuestFee = guestFee;
        FemaleDiscountMonthly = femaleDiscountMonthly;
        FemaleDiscountDaily = femaleDiscountDaily;
        FemaleDiscountGuest = femaleDiscountGuest;
        RoundRule = roundRule;
        AppliedAt = DateTime.UtcNow;
    }

    public void UpdateFees(
        string feeMode,
        decimal monthlyFee,
        decimal dailyMemberFee,
        decimal guestFee,
        decimal femaleDiscountMonthly,
        decimal femaleDiscountDaily,
        decimal femaleDiscountGuest,
        int roundRule)
    {
        UpdateFees(ParseFeeMode(feeMode), monthlyFee, dailyMemberFee, guestFee, femaleDiscountMonthly, femaleDiscountDaily, femaleDiscountGuest, roundRule);
    }

    public Result<decimal> CalculateMonthlyContribution(short gender)
    {
        if (!Enum.IsDefined((Gender)gender))
            return Result.Failure<decimal>(Error.Validation("Ledger.InvalidGender", "Giới tính không hợp lệ."));
        var discount = gender == (short)Gender.Female ? FemaleDiscountMonthly : 0m;
        if (!Services.LedgerBalance.IsValidAmount(MonthlyFee) || discount < 0 ||
            decimal.Round(discount, 2) != discount || discount > MonthlyFee ||
            !Services.LedgerBalance.IsValidAmount(MonthlyFee - discount))
            return Result.Failure<decimal>(Error.Conflict("Ledger.MonthlyPolicyUnsupported", "Biểu phí không hợp lệ hoặc chưa hỗ trợ miễn phí tháng."));
        return Result.Success(MonthlyFee - discount);
    }

    private static FeeMode ParseFeeMode(string feeMode)
    {
        return Enum.TryParse<FeeMode>(feeMode, true, out var result) ? result : FeeMode.DYNAMIC;
    }
}
