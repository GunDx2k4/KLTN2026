using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Services;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Settlement;

internal static class SessionSettlementMapper
{
    public static SettlementCosts Costs(PreviewSettlementRequest input) =>
        new(input.ShuttlecockUsed, input.ShuttlecockPrice, input.CourtRentalCost, input.ExtraExpense);

    public static SettlementAdjustment[] Adjustments(PreviewSettlementRequest input) =>
        input.Overrides.Select(x => new SettlementAdjustment(x.UserId, x.FinalAmount, x.Reason)).ToArray();

    public static SessionSettlementResponse Map(BadmintonSession session)
    {
        var published = session.PublishedPreviewId.HasValue;
        var fees = session.Fees.Where(f => !f.IsDeleted).OrderBy(f => f.UserId.ToString("N"), StringComparer.Ordinal)
            .Select(f => new MemberSettlementFeeResponse(f.Id, f.UserId, f.User?.FullName ?? "", f.User?.AvatarUrl,
                f.PriceType.ToString(), f.Amount, f.PaymentStatus.ToString(), f.QrCodePayload, f.PaymentCode,
                f.PaidAt, f.PaymentMethod?.ToString(), f.IsPublished ? new SettlementMemberAllocation(f.UserId,
                    f.Gender!.Value, f.MemberType!, f.MonthlyExpiryDate, f.PriceType, f.CourtShare!.Value, f.ShuttleShare!.Value,
                    f.ExtraShare!.Value, f.MonthlyFundCovered!.Value, f.BaseAmount!.Value, f.DiscountApplied, f.RawAmount!.Value,
                    f.CalculatedAmount!.Value, f.Amount, f.AdjustmentReason, f.RoundingDelta!.Value, f.AdjustmentDelta!.Value) : null)).ToArray();
        return new(session.Id, published ? "PUBLISHED" : fees.Length > 0 ? "LEGACY" : "UNPUBLISHED",
            session.ShuttlecockUsed, published ? session.ShuttlecockPrice : null, session.CourtRentalCost,
            session.ExtraExpense, published ? session.TotalCost : null, published ? session.TotalReceivable : null,
            fees.Where(f => f.PaymentStatus == nameof(PaymentStatus.PAID)).Sum(f => f.FinalFee),
            published ? session.MonthlyFundCovered : null, published ? session.TotalDiscount : null, published ? session.PolicyDelta : null,
            published ? session.RoundingDelta : null, published ? session.AdjustmentDelta : null,
            published ? session.BalanceDifference : null, session.PublishedAt, session.PublishedBy, fees);
    }
}
