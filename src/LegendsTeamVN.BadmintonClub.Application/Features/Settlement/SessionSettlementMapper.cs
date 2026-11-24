using LegendsTeamVN.BadmintonClub.Application.DTOs.Settlement.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Settlement.Responses;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Services;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Settlement;

internal static class SessionSettlementMapper
{
    public static SettlementCosts Costs(PreviewSettlementRequest input) =>
        new(input.ShuttlecockUsed, input.ShuttlecockPrice, input.CourtRentalCost, input.ExtraExpense, input.MonthlyExemptCourtOnly);

    public static SettlementAdjustment[] Adjustments(PreviewSettlementRequest input) =>
        input.Overrides.Select(x => new SettlementAdjustment(x.UserId, x.FinalAmount, x.Reason)).ToArray();

    public static SessionSettlementResponse Map(BadmintonSession session)
    {
        var published = session.PublishedPreviewId.HasValue;
        var fees = session.Fees.Where(f => !f.IsDeleted).OrderBy(f => f.UserId.ToString("N"), StringComparer.Ordinal)
            .Select(f => new MemberSettlementFeeResponse(f.Id, f.UserId, f.User?.FullName ?? "", f.User?.AvatarUrl,
                f.PriceType.ToString(), f.Amount, f.PaymentStatus.ToString(), f.QrCodePayload, f.PaymentCode,
                f.PaidAt, f.PaymentMethod?.ToString(), f.IsPublished ? new SettlementMemberAllocation(f.UserId,
                    f.Gender ?? 0, f.MemberType ?? "GUEST", f.MonthlyExpiryDate, f.PriceType, f.CourtShare ?? 0m, f.ShuttleShare ?? 0m,
                    f.ExtraShare ?? 0m, f.MonthlyFundCovered ?? 0m, f.BaseAmount ?? 0m, f.DiscountApplied, f.RawAmount ?? 0m,
                    f.CalculatedAmount ?? 0m, f.Amount, f.AdjustmentReason, f.RoundingDelta ?? 0m, f.AdjustmentDelta ?? 0m) : null,
                f.PaymentProgress?.ToString())).ToArray();

        var activeBank = session.Group?.BankAccounts?.FirstOrDefault(b => b.IsActive);
        var bankResponse = activeBank is not null
            ? new PaymentBankAccountResponse(activeBank.Id, activeBank.BankBin, activeBank.BankName, activeBank.AccountNumber, activeBank.AccountHolder)
            : null;

        return new(session.Id, published ? "PUBLISHED" : fees.Length > 0 ? "LEGACY" : "UNPUBLISHED",
            session.ShuttlecockUsed, published ? session.ShuttlecockPrice : null, session.CourtRentalCost,
            session.ExtraExpense, published ? session.TotalCost : null, published ? session.TotalReceivable : null,
            session.CalculateCollectedAmount(),
            published ? session.MonthlyFundCovered : null, published ? session.TotalDiscount : null, published ? session.PolicyDelta : null,
            published ? session.RoundingDelta : null, published ? session.AdjustmentDelta : null,
            published ? session.BalanceDifference : null, session.PublishedAt, session.PublishedBy, fees, bankResponse);
    }
}
