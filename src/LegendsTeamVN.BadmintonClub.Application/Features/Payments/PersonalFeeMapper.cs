using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments;

internal static class PersonalFeeMapper
{
    public static PersonalFeeResponse Map(SessionFee f, bool enabled, bool writes)
    {
        var unpaid = f.PaymentStatus == PaymentStatus.UNPAID;
        var active = !f.Session.IsDeleted && !f.Session.Group.IsDeleted && f.Session.Group.IsActive;
        var actions = new List<string>();
        if (enabled && writes && active && unpaid && f.IsPublished && f.Session.PublishedPreviewId.HasValue)
        {
            if (f.PaymentMethod is null or PaymentMethod.CASH) actions.Add("CASH_REQUEST");
            if (f.PaymentMethod is null or PaymentMethod.BANK_TRANSFER) actions.Add("BANK_TRANSFER");
            if (f.PaymentMethod == PaymentMethod.BANK_TRANSFER && f.PaymentRequestId.HasValue &&
                f.PaymentProgress == PaymentProgress.QR_READY && !string.IsNullOrWhiteSpace(f.QrCodePayload)) actions.Add("TRANSFER_NOTICE");
        }
        var receipt = f.PaymentStatus == PaymentStatus.PAID ? new PaymentReceipt(f.Id, f.Amount, f.PaymentCode, f.PaidAt,
            f.PaymentMethod?.ToString(), f.CashCollectedBy, f.PaidAt is null || f.PaymentMethod is null) : null;
        var instructions = enabled && active && f.ValidateQrAccess().IsSuccess
            ? new PaymentInstructions(f.QrCodePayload, f.GetPaymentBankSnapshot()!, $"/api/v1/user/me/fees/{f.Id}/qr.png") : null;
        return new(f.Id, f.SessionId, f.Session.GroupId, f.Session.Group.GroupName, f.Session.StartTime,
            f.IsPublished ? "PUBLISHED" : "LEGACY", f.Amount, "VND", f.PaymentCode, f.PaymentStatus.ToString(),
            f.PaymentProgress?.ToString(), f.PaymentMethod?.ToString(), f.PaymentRequestedAt, f.TransferReportedAt,
            f.IsPublished ? new(f.CourtShare, f.ShuttleShare, f.ExtraShare, f.DiscountApplied, f.MonthlyFundCovered,
                f.BaseAmount, f.CalculatedAmount, f.RoundingDelta, f.AdjustmentDelta, f.AdjustmentReason) : null,
            actions, instructions, receipt);
    }
}
