using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Settlement;

public sealed class SessionSettlementHandler(
    ISessionRepository sessions,
    IClubRepository clubs,
    IClubMemberRepository clubMembers,
    ICurrentUserService currentUser,
    TimeProvider clock
) : ICommandHandler<CalculateSessionSettlementCommand, SessionSettlementResponse>,
    IQueryHandler<GetSessionSettlementQuery, SessionSettlementResponse>,
    ICommandHandler<MarkMemberPaidCommand, SessionSettlementResponse>
{
    public async Task<Result<SessionSettlementResponse>> Handle(CalculateSessionSettlementCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actorId)
            return Result.Failure<SessionSettlementResponse>(Error.Unauthorized("User.Unauthorized", "Vui lÃƒÂ²ng Ã„â€˜Ã„Æ’ng nhÃ¡ÂºÂ­p."));

        var session = await sessions.GetSettlementAsync(request.SessionId, ct);
        if (session is null)
            return Result.Failure<SessionSettlementResponse>(Error.NotFound("Session.NotFound", "KhÃƒÂ´ng tÃƒÂ¬m thÃ¡ÂºÂ¥y buÃ¡Â»â€¢i chÃ†Â¡i."));

        if (!await sessions.CanManageFinanceAsync(session.GroupId, actorId, ct))
            return Result.Failure<SessionSettlementResponse>(Error.Forbidden("Settlement.Forbidden", "BÃ¡ÂºÂ¡n khÃƒÂ´ng cÃƒÂ³ quyÃ¡Â»Ân quÃ¡ÂºÂ£n lÃƒÂ½ tÃƒÂ i chÃƒÂ­nh buÃ¡Â»â€¢i chÃ†Â¡i."));

        var club = await clubs.FindAll(c => c.Id == session.GroupId)
            .Include(c => c.FeeRules)
            .Include(c => c.BankAccounts)
            .FirstOrDefaultAsync(ct);

        var feeRule = club?.FeeRules.OrderByDescending(r => r.AppliedAt).FirstOrDefault();
        var bankAccount = club?.BankAccounts.FirstOrDefault(b => b.IsActive);

        var members = await clubMembers.FindAll(m => m.GroupId == session.GroupId && !m.IsDeleted)
            .Include(m => m.RoleEntity)
            .ToListAsync(ct);

        var memberRoleDict = members.ToDictionary(m => m.UserId, m => m.RoleEntity?.RoleName ?? "ThÃƒÂ nh viÃƒÂªn");

        // CÃ¡ÂºÂ­p nhÃ¡ÂºÂ­t chi phÃƒÂ­ vÃƒÂ o Session
        var courtCost = request.CourtFee ?? session.CourtRentalCost;
        session.UpdateExpenses(request.ShuttlecockUsed, courtCost, request.ExtraFee);
        if (session.Status is SessionStatus.OPEN or SessionStatus.LOCKED or SessionStatus.PLAYING)
        {
            session.UpdateStatus(SessionStatus.CALCULATED);
        }

        // LÃ¡ÂºÂ¥y danh sÃƒÂ¡ch ngÃ†Â°Ã¡Â»Âi chÃ†Â¡i tham gia (CONFIRMED hoÃ¡ÂºÂ·c CHECKED_IN)
        var confirmedBookings = session.Bookings
            .Where(b => !b.IsDeleted && b.Status is BookingStatus.CONFIRMED or BookingStatus.CHECKED_IN)
            .ToList();

        var totalPlayers = confirmedBookings.Count > 0 ? confirmedBookings.Count : (session.MaxPlayers > 0 ? session.MaxPlayers : 1);
        var femaleCount = confirmedBookings.Count(b => b.User.Gender == 2);

        var shuttleTotal = request.ShuttlecockUsed * request.ShuttlecockPrice;
        var totalCost = (int)Math.Round(courtCost + shuttleTotal + request.ExtraFee);
        var femaleDiscount = feeRule != null ? (int)feeRule.FemaleDiscountDaily : 10000;
        var roundRule = feeRule != null && feeRule.RoundRule > 0 ? feeRule.RoundRule : 1000;

        // CÃƒÂ´ng thÃ¡Â»Â©c chia tiÃ¡Â»Ân Ã„â€˜a hÃ¡Â»â€¡ sÃ¡Â»â€˜ bÃƒÂ¹ chÃƒÂ©o
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

        // CÃ¡ÂºÂ­p nhÃ¡ÂºÂ­t hoÃ¡ÂºÂ·c tÃ¡ÂºÂ¡o SessionFee cho tÃ¡Â»Â«ng ngÃ†Â°Ã¡Â»Âi chÃ†Â¡i
        foreach (var booking in confirmedBookings)
        {
            var u = booking.User;
            var isFemale = u.Gender == 2;
            var roleName = memberRoleDict.TryGetValue(u.Id, out var rName) ? rName : "KhÃƒÂ¡ch vÃƒÂ£ng lai";
            var isMonthly = roleName.Contains("thÃƒÂ¡ng", StringComparison.OrdinalIgnoreCase) || roleName.Contains("Monthly", StringComparison.OrdinalIgnoreCase);

            var baseFee = isMonthly ? 0 : maleFee;
            var finalFee = isMonthly ? 0 : (isFemale ? femaleFee : maleFee);
            var discount = isMonthly ? maleFee : (isFemale ? (maleFee - femaleFee) : 0);

            var priceType = isMonthly ? PriceType.MONTHLY_MEMBER : (roleName.Contains("KhÃƒÂ¡ch", StringComparison.OrdinalIgnoreCase) ? PriceType.GUEST : PriceType.DAILY_MEMBER);
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
                }
            }
        }

        sessions.Update(session);

        return await BuildResponseAsync(session.Id, ct);
    }

    public async Task<Result<SessionSettlementResponse>> Handle(GetSessionSettlementQuery request, CancellationToken ct)
    {
        return await BuildResponseAsync(request.SessionId, ct);
    }

    public async Task<Result<SessionSettlementResponse>> Handle(MarkMemberPaidCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actorId)
            return Result.Failure<SessionSettlementResponse>(Error.Unauthorized("User.Unauthorized", "Vui lÃƒÂ²ng Ã„â€˜Ã„Æ’ng nhÃ¡ÂºÂ­p."));

        var session = await sessions.GetSettlementAsync(request.SessionId, ct);
        if (session is null)
            return Result.Failure<SessionSettlementResponse>(Error.NotFound("Session.NotFound", "KhÃƒÂ´ng tÃƒÂ¬m thÃ¡ÂºÂ¥y buÃ¡Â»â€¢i chÃ†Â¡i."));

        if (!await sessions.CanManageFinanceAsync(session.GroupId, actorId, ct))
            return Result.Failure<SessionSettlementResponse>(Error.Forbidden("Settlement.Forbidden", "BÃ¡ÂºÂ¡n khÃƒÂ´ng cÃƒÂ³ quyÃ¡Â»Ân quÃ¡ÂºÂ£n lÃƒÂ½ tÃƒÂ i chÃƒÂ­nh buÃ¡Â»â€¢i chÃ†Â¡i."));

        var fee = session.Fees.FirstOrDefault(f => f.UserId == request.UserId && !f.IsDeleted);
        if (fee is not null)
        {
            fee.MarkAsPaid(PaymentMethod.CASH, actorId);
            sessions.Update(session);
        }

        return await BuildResponseAsync(session.Id, ct);
    }

    private async Task<Result<SessionSettlementResponse>> BuildResponseAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await sessions.GetSettlementAsync(sessionId, ct);
        if (session is null)
            return Result.Failure<SessionSettlementResponse>(Error.NotFound("Session.NotFound", "KhÃƒÂ´ng tÃƒÂ¬m thÃ¡ÂºÂ¥y buÃ¡Â»â€¢i chÃ†Â¡i."));

        var members = await clubMembers.FindAll(m => m.GroupId == session.GroupId && !m.IsDeleted)
            .Include(m => m.RoleEntity)
            .ToListAsync(ct);

        var memberRoleDict = members.ToDictionary(m => m.UserId, m => m.RoleEntity?.RoleName ?? "ThÃƒÂ nh viÃƒÂªn");

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
            var roleName = memberRoleDict.TryGetValue(u.Id, out var rName) ? rName : "KhÃƒÂ¡ch vÃƒÂ£ng lai";
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
