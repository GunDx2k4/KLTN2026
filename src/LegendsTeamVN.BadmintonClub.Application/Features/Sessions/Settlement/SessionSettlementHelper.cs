using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Settlement;

internal static class SessionSettlementHelper
{
    public static async Task<Result<SessionSettlementResponse>> BuildResponseAsync(
        ISessionRepository sessions,
        IClubMemberRepository clubMembers,
        Guid sessionId,
        CancellationToken ct)
    {
        var session = await sessions.GetSettlementAsync(sessionId, ct);
        if (session is null)
            return Result.Failure<SessionSettlementResponse>(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));

        var members = await clubMembers.FindAll(m => m.GroupId == session.GroupId && !m.IsDeleted)
            .Include(m => m.RoleEntity)
            .ToListAsync(ct);

        var memberRoleDict = members.ToDictionary(m => m.UserId, m => m.RoleEntity?.RoleName ?? "Thành viên");

        var shuttlePrice = 25000;
        var shuttleTotal = session.ShuttlecockUsed * shuttlePrice;
        var courtCost = (int)Math.Round(session.CourtRentalCost);
        var extraFee = (int)Math.Round(session.ExtraExpense);
        var totalCost = courtCost + shuttleTotal + extraFee;

        var confirmedBookings = session.Bookings
            .Where(b => !b.IsDeleted && b.Status is BookingStatus.CONFIRMED or BookingStatus.CHECKED_IN)
            .ToList();

        var memberFees = new List<MemberSettlementFeeResponse>();

        foreach (var booking in confirmedBookings)
        {
            var u = booking.User;
            var fee = session.Fees.FirstOrDefault(f => f.UserId == u.Id && !f.IsDeleted);
            var roleName = memberRoleDict.TryGetValue(u.Id, out var rName) ? rName : "Khách vãng lai";
            var genderStr = (u.Gender == 2) ? "FEMALE" : "MALE";

            var baseFee = fee != null ? (int)Math.Round(fee.Amount + fee.DiscountApplied) : 0;
            var finalFee = fee != null ? (int)Math.Round(fee.Amount) : 0;
            var discount = fee != null ? (int)Math.Round(fee.DiscountApplied) : 0;
            var statusStr = fee != null ? (fee.PaymentStatus == PaymentStatus.PAID ? "PAID" : "PENDING") : "PENDING";
            var qrUrl = fee?.QrCodePayload ?? "";
            var paymentCode = fee?.PaymentCode ?? "";

            memberFees.Add(new MemberSettlementFeeResponse(
                u.Id,
                u.FullName,
                u.AvatarUrl,
                roleName,
                genderStr,
                baseFee,
                discount,
                finalFee,
                statusStr,
                qrUrl,
                paymentCode
            ));
        }

        var totalCollected = memberFees.Where(m => m.PaymentStatus == "PAID").Sum(m => m.FinalFee);

        var response = new SessionSettlementResponse(
            session.Id,
            session.ShuttlecockUsed,
            shuttlePrice,
            courtCost,
            extraFee,
            totalCost,
            totalCollected,
            memberFees,
            session.Status is SessionStatus.CALCULATED or SessionStatus.COMPLETED
        );

        return Result.Success(response);
    }
}
