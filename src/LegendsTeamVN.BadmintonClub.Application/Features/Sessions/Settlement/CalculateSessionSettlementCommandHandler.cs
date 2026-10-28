using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Settlement;

public sealed class CalculateSessionSettlementCommandHandler(
    ISessionRepository sessions,
    IClubRepository clubs,
    IClubMemberRepository clubMembers,
    ICurrentUserService currentUser
) : ICommandHandler<CalculateSessionSettlementCommand, SessionSettlementResponse>
{
    public async Task<Result<SessionSettlementResponse>> Handle(CalculateSessionSettlementCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actorId)
            return Result.Failure<SessionSettlementResponse>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));

        var session = await sessions.GetSettlementAsync(request.SessionId, true, ct);
        if (session is null)
            return Result.Failure<SessionSettlementResponse>(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));

        if (!await sessions.CanManageFinanceAsync(session.GroupId, actorId, ct))
            return Result.Failure<SessionSettlementResponse>(Error.Forbidden("Settlement.Forbidden", "Bạn không có quyền quản lý tài chính buổi chơi."));

        if (session.Status is SessionStatus.OPEN or SessionStatus.LOCKED or SessionStatus.PLAYING)
        {
            var closed = session.ChangeOperationalStatus(SessionStatus.CALCULATED, false, actorId, DateTime.UtcNow);
            if (closed.IsFailure)
                return Result.Failure<SessionSettlementResponse>(closed.Error);
        }

        var club = await clubs.FindAll(c => c.Id == session.GroupId)
            .Include(c => c.FeeRules)
            .Include(c => c.BankAccounts)
            .FirstOrDefaultAsync(ct);

        var feeRule = club?.FeeRules.OrderByDescending(r => r.AppliedAt).FirstOrDefault();
        var bankAccount = club?.BankAccounts.FirstOrDefault(b => b.IsActive);

        var members = await clubMembers.FindAll(m => m.GroupId == session.GroupId && !m.IsDeleted)
            .Include(m => m.RoleEntity)
            .ToListAsync(ct);

        var memberRoleDict = members.ToDictionary(m => m.UserId, m => m.RoleEntity?.RoleName ?? "Thành viên");

        var courtCost = request.CourtFee ?? session.CourtRentalCost;
        session.UpdateExpenses(request.ShuttlecockUsed, courtCost, request.ExtraFee);

        var confirmedBookings = session.Bookings
            .Where(b => !b.IsDeleted && b.Status is BookingStatus.CONFIRMED or BookingStatus.CHECKED_IN)
            .ToList();

        var totalPlayers = confirmedBookings.Count > 0 ? confirmedBookings.Count : (session.MaxPlayers > 0 ? session.MaxPlayers : 1);
        var femaleCount = confirmedBookings.Count(b => b.User.Gender == 2);

        var shuttleTotal = request.ShuttlecockUsed * request.ShuttlecockPrice;
        var totalCost = (int)Math.Round(courtCost + shuttleTotal + request.ExtraFee);
        var femaleDiscount = feeRule != null ? (int)feeRule.FemaleDiscountDaily : 10000;
        var roundRule = feeRule != null && feeRule.RoundRule > 0 ? feeRule.RoundRule : 1000;

        int maleFee;
        int femaleFee;
        if (totalPlayers <= 0)
        {
            maleFee = 0;
            femaleFee = 0;
        }
        else if (femaleCount >= totalPlayers)
        {
            var rawFee = (double)totalCost / totalPlayers;
            femaleFee = (int)Math.Ceiling(rawFee / roundRule) * roundRule;
            maleFee = femaleFee;
        }
        else
        {
            var rawMale = (double)(totalCost + (femaleCount * femaleDiscount)) / totalPlayers;
            maleFee = (int)Math.Ceiling(rawMale / roundRule) * roundRule;
            femaleFee = Math.Max(0, maleFee - femaleDiscount);
        }

        var addedFees = new List<SessionFee>();
        foreach (var booking in confirmedBookings)
        {
            var u = booking.User;
            var isFemale = u.Gender == 2;
            var roleName = memberRoleDict.TryGetValue(u.Id, out var rName) ? rName : "Khách vãng lai";
            var isMonthly = roleName.Contains("tháng", StringComparison.OrdinalIgnoreCase) || roleName.Contains("Monthly", StringComparison.OrdinalIgnoreCase);

            var finalFee = isMonthly ? 0 : (isFemale ? femaleFee : maleFee);
            var discount = isMonthly ? maleFee : (isFemale ? (maleFee - femaleFee) : 0);

            var priceType = isMonthly ? PriceType.MONTHLY_MEMBER : (roleName.Contains("Khách", StringComparison.OrdinalIgnoreCase) ? PriceType.GUEST : PriceType.DAILY_MEMBER);
            var paymentCode = $"PAY_{session.Id.ToString()[..4].ToUpper()}_{u.Id.ToString()[..4].ToUpper()}_{finalFee / 1000}K";

            var bankBin = !string.IsNullOrWhiteSpace(bankAccount?.BankName) ? bankAccount.BankName : "MB";
            var bankAcc = !string.IsNullOrWhiteSpace(bankAccount?.AccountNumber) ? bankAccount.AccountNumber : "034888888";
            var vietQrUrl = finalFee > 0 ? $"https://img.vietqr.io/image/{bankBin}-{bankAcc}-compact2.png?amount={finalFee}&addInfo={paymentCode}" : "";

            var existingFee = session.Fees.FirstOrDefault(f => f.UserId == u.Id && !f.IsDeleted);
            if (existingFee is null)
            {
                var newFee = new SessionFee(
                    session.Id,
                    u.Id,
                    finalFee,
                    priceType,
                    paymentCode,
                    vietQrUrl,
                    null,
                    discount,
                    isMonthly ? PaymentStatus.EXEMPT : PaymentStatus.UNPAID
                );
                session.Fees.Add(newFee);
                addedFees.Add(newFee);
            }
            else
            {
                if (existingFee.PaymentStatus != PaymentStatus.PAID)
                {
                    session.Fees.Remove(existingFee);
                    var updatedFee = new SessionFee(
                        session.Id,
                        u.Id,
                        finalFee,
                        priceType,
                        paymentCode,
                        vietQrUrl,
                        existingFee.PaymentMethod,
                        discount,
                        isMonthly ? PaymentStatus.EXEMPT : PaymentStatus.UNPAID
                    );
                    session.Fees.Add(updatedFee);
                    addedFees.Add(updatedFee);
                }
            }
        }

        sessions.AddSettlementFees(addedFees);

        return await SessionSettlementHelper.BuildResponseAsync(session, clubMembers, ct);
    }
}
