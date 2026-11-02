using System.Collections.ObjectModel;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Domain.Services;

public static class SettlementAllocationEngine
{
    public const decimal MaximumAmount = 9999999999999999.99m;

    public static bool IsValidAmount(decimal amount) => amount >= 0 && amount <= MaximumAmount && decimal.Round(amount, 2) == amount;

    public static Result<SettlementAllocation> Calculate(
        BadmintonSession session,
        GroupFeeRule rule,
        IReadOnlyCollection<AppUser> users,
        IReadOnlyCollection<GroupMember> memberships,
        SettlementCosts costs,
        IReadOnlyCollection<SettlementAdjustment> adjustments,
        DateTime now,
        bool confirmRemainingNoShows = false)
    {
        if (session is null || rule is null || users is null || memberships is null || costs is null || adjustments is null ||
            users.Any(u => u is null) || memberships.Any(m => m is null) || adjustments.Any(a => a is null))
            return Fail("Settlement.InvalidRequest", "Dữ liệu quyết toán không được để trống.");
        var allowed = session.ValidateSettlement(now, confirmRemainingNoShows);
        if (allowed.IsFailure) return Result.Failure<SettlementAllocation>(allowed.Error);
        if (rule.GroupId != session.GroupId || !Enum.IsDefined(rule.FeeMode) || rule.RoundRule <= 0 ||
            !IsValidAmount(rule.MonthlyFee) || !IsValidAmount(rule.DailyMemberFee) || !IsValidAmount(rule.GuestFee) ||
            !IsValidAmount(rule.FemaleDiscountMonthly) || !IsValidAmount(rule.FemaleDiscountDaily) || !IsValidAmount(rule.FemaleDiscountGuest))
            return Fail("Settlement.InvalidRule", "Biểu phí không hợp lệ hoặc không thuộc câu lạc bộ của buổi chơi.");
        if (costs.ShuttlecockUsed < 0 || !IsValidAmount(costs.ShuttlecockPrice) ||
            !IsValidAmount(costs.CourtRentalCost) || !IsValidAmount(costs.ExtraExpense))
            return Fail("Settlement.InvalidCosts", "Số cầu và chi phí phải không âm, đúng độ chính xác và giới hạn lưu trữ.");

        var bookings = session.Bookings.Where(b => !b.IsDeleted && b.Status == BookingStatus.CHECKED_IN)
            .OrderBy(b => b.UserId.ToString("N"), StringComparer.Ordinal).ToArray();
        if (bookings.Length == 0 || bookings.Any(b => b.SessionId != session.Id) ||
            bookings.Select(b => b.UserId).Distinct().Count() != bookings.Length)
            return Fail("Settlement.InvalidParticipants", "Buổi chơi cần danh sách người có mặt duy nhất và hợp lệ.");
        var userIds = bookings.Select(b => b.UserId).ToHashSet();
        if (adjustments.Any(a => !userIds.Contains(a.UserId) || !IsValidAmount(a.FinalAmount) || string.IsNullOrWhiteSpace(a.Reason)) ||
            adjustments.Select(a => a.UserId).Distinct().Count() != adjustments.Count)
            return Fail("Settlement.InvalidAdjustment", "Điều chỉnh phải thuộc người có mặt, không trùng, có số tiền hợp lệ và lý do.");
        var missingExpiry = memberships.Where(m => m.GroupId == session.GroupId && userIds.Contains(m.UserId) &&
            !m.IsDeleted && m.Status == ClubMemberStatus.ACTIVE && m.MemberType == "MONTHLY" &&
            !m.MonthlyExpiryDate.HasValue).Select(m => m.UserId).Distinct()
            .OrderBy(id => id.ToString("N"), StringComparer.Ordinal).ToArray();
        if (missingExpiry.Length > 0)
            return Fail("Settlement.MonthlyExpiryRequired",
                $"Hội viên tháng chưa có ngày hết hạn được xác nhận: {string.Join(", ", missingExpiry)}.");
        var adjustmentsByUser = adjustments.ToDictionary(a => a.UserId);

        try
        {
            var shuttleCost = costs.ShuttlecockUsed * costs.ShuttlecockPrice;
            var totalCost = costs.CourtRentalCost + shuttleCost + costs.ExtraExpense;
            if (!IsValidAmount(shuttleCost) || !IsValidAmount(totalCost))
                return Fail("Settlement.AmountOutOfRange", "Tổng chi phí vượt giới hạn lưu trữ.");
            var courtShares = Allocate(costs.CourtRentalCost, bookings.Length);
            var shuttleShares = Allocate(shuttleCost, bookings.Length);
            var extraShares = Allocate(costs.ExtraExpense, bookings.Length);
            var lines = new List<SettlementMemberAllocation>();
            for (var index = 0; index < bookings.Length; index++)
            {
                var userId = bookings[index].UserId;
                var matchingUsers = users.Where(u => u.Id == userId).ToArray();
                if (matchingUsers.Length != 1 || matchingUsers[0].IsDeleted ||
                    !Enum.IsDefined((Gender)matchingUsers[0].Gender))
                    return Fail("Settlement.InvalidUser", "Thiếu tài khoản hoặc giới tính hợp lệ của người chơi.");
                var user = matchingUsers[0];
                var matchingMembers = memberships.Where(m => m.GroupId == session.GroupId && m.UserId == userId &&
                    !m.IsDeleted && m.Status == ClubMemberStatus.ACTIVE).ToArray();
                if (matchingMembers.Length > 1)
                    return Fail("Settlement.InvalidMembership", "Dữ liệu thành viên bị trùng trong câu lạc bộ.");
                var member = matchingMembers.SingleOrDefault();
                var priceType = ResolvePriceType(member, session.StartTime);
                if (priceType.IsFailure) return Result.Failure<SettlementAllocation>(priceType.Error with
                {
                    Message = $"{priceType.Error.Message} Người chơi: {userId}."
                });
                var actualShare = courtShares[index] + shuttleShares[index] + extraShares[index];
                var monthlyCovered = priceType.Value == PriceType.MONTHLY_MEMBER ? actualShare : 0m;
                var baseAmount = priceType.Value == PriceType.MONTHLY_MEMBER ? 0m : rule.FeeMode == FeeMode.DYNAMIC
                    ? actualShare : priceType.Value == PriceType.GUEST ? rule.GuestFee : rule.DailyMemberFee;
                var configuredDiscount = user.Gender != (short)Gender.Female ? 0m : priceType.Value switch
                {
                    PriceType.DAILY_MEMBER => rule.FemaleDiscountDaily,
                    PriceType.GUEST => rule.FemaleDiscountGuest,
                    _ => 0m
                };
                var discount = decimal.Min(baseAmount, configuredDiscount);
                var rawAmount = baseAmount - discount;
                var calculatedAmount = decimal.Ceiling(rawAmount / rule.RoundRule) * rule.RoundRule;
                var adjustment = adjustmentsByUser.GetValueOrDefault(userId);
                var finalAmount = adjustment?.FinalAmount ?? calculatedAmount;
                if (!IsValidAmount(calculatedAmount))
                    return Fail("Settlement.AmountOutOfRange", "Khoản thu sau làm tròn vượt giới hạn lưu trữ.");
                lines.Add(new SettlementMemberAllocation(userId, user.Gender, member?.MemberType ?? "GUEST",
                    member?.MonthlyExpiryDate, priceType.Value, courtShares[index], shuttleShares[index], extraShares[index],
                    monthlyCovered, baseAmount, discount, rawAmount, calculatedAmount, finalAmount,
                    adjustment?.Reason.Trim(), calculatedAmount - rawAmount, finalAmount - calculatedAmount));
            }
            var totalReceivable = lines.Sum(l => l.FinalAmount);
            var monthlyFundCovered = lines.Sum(l => l.MonthlyFundCovered);
            var totalDiscount = lines.Sum(l => l.DiscountApplied);
            var roundingDelta = lines.Sum(l => l.RoundingDelta);
            var adjustmentDelta = lines.Sum(l => l.AdjustmentDelta);
            var policyDelta = lines.Sum(l => l.RawAmount) + monthlyFundCovered - totalCost;
            var balanceDifference = totalReceivable + monthlyFundCovered - totalCost;
            if (!IsValidAmount(totalReceivable) || !IsValidAmount(monthlyFundCovered) || !IsValidAmount(totalDiscount) ||
                !IsSignedAmount(roundingDelta) || !IsSignedAmount(adjustmentDelta) || !IsSignedAmount(policyDelta) || !IsSignedAmount(balanceDifference))
                return Fail("Settlement.AmountOutOfRange", "Tổng khoản thu hoặc chênh lệch vượt giới hạn lưu trữ.");
            var ruleSnapshot = new SettlementRuleSnapshot(rule.Id, rule.FeeMode, rule.DailyMemberFee, rule.GuestFee,
                rule.FemaleDiscountDaily, rule.FemaleDiscountGuest, rule.RoundRule, rule.AppliedAt);
            return Result.Success(new SettlementAllocation(session.Id, session.GroupId, costs, ruleSnapshot, shuttleCost,
                totalCost, totalReceivable, monthlyFundCovered, totalDiscount, policyDelta, roundingDelta, adjustmentDelta,
                balanceDifference, new ReadOnlyCollection<SettlementMemberAllocation>(lines)));
        }
        catch (OverflowException)
        {
            return Fail("Settlement.AmountOutOfRange", "Phép tính chi phí vượt giới hạn lưu trữ.");
        }
    }

    private static Result<PriceType> ResolvePriceType(GroupMember? member, DateTime startTime)
    {
        if (member is null || member.MemberType == "GUEST") return Result.Success(PriceType.GUEST);
        if (member.MemberType == "DAILY") return Result.Success(PriceType.DAILY_MEMBER);
        if (member.MemberType != "MONTHLY")
            return Result.Failure<PriceType>(Error.Validation("Settlement.InvalidMembership", "Loại hội viên không hợp lệ."));
        if (!member.MonthlyExpiryDate.HasValue)
            return Result.Failure<PriceType>(Error.Validation("Settlement.MonthlyExpiryRequired", "Hội viên tháng chưa có ngày hết hạn được xác nhận."));
        if (member.MonthlyExpiryDate.Value.Kind != DateTimeKind.Utc)
            return Result.Failure<PriceType>(Error.Validation("Settlement.UtcRequired", "Ngày hết hạn hội viên tháng phải sử dụng UTC."));
        return Result.Success(member.MonthlyExpiryDate >= startTime ? PriceType.MONTHLY_MEMBER : PriceType.DAILY_MEMBER);
    }

    private static decimal[] Allocate(decimal amount, int count)
    {
        var units = amount * 100m;
        var baseUnits = decimal.Floor(units / count);
        var remaining = units % count;
        return Enumerable.Range(0, count).Select(i => (baseUnits + (i < remaining ? 1m : 0m)) / 100m).ToArray();
    }

    private static bool IsSignedAmount(decimal amount) => amount >= -MaximumAmount && amount <= MaximumAmount && decimal.Round(amount, 2) == amount;
    private static Result<SettlementAllocation> Fail(string code, string message) => Result.Failure<SettlementAllocation>(Error.Validation(code, message));
}

public sealed record SettlementCosts(int ShuttlecockUsed, decimal ShuttlecockPrice, decimal CourtRentalCost, decimal ExtraExpense);
public sealed record SettlementAdjustment(Guid UserId, decimal FinalAmount, string Reason);
public sealed record SettlementRuleSnapshot(Guid Id, FeeMode FeeMode, decimal DailyMemberFee, decimal GuestFee,
    decimal FemaleDiscountDaily, decimal FemaleDiscountGuest, int RoundRule, DateTime AppliedAt);
public sealed record SettlementMemberAllocation(Guid UserId, short Gender, string MemberType, DateTime? MonthlyExpiryDate,
    PriceType PriceType, decimal CourtShare, decimal ShuttleShare, decimal ExtraShare, decimal MonthlyFundCovered,
    decimal BaseAmount, decimal DiscountApplied, decimal RawAmount, decimal CalculatedAmount, decimal FinalAmount,
    string? AdjustmentReason, decimal RoundingDelta, decimal AdjustmentDelta);
public sealed record SettlementAllocation(Guid SessionId, Guid GroupId, SettlementCosts Costs, SettlementRuleSnapshot Rule,
    decimal ShuttleCost, decimal TotalCost, decimal TotalReceivable, decimal MonthlyFundCovered, decimal TotalDiscount,
    decimal PolicyDelta, decimal RoundingDelta, decimal AdjustmentDelta, decimal BalanceDifference,
    IReadOnlyList<SettlementMemberAllocation> Members);
