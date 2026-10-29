using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Settlement;

public sealed class GetSettlementFormQueryHandler(ISessionRepository sessions, ICurrentUserService currentUser)
    : IQueryHandler<GetSettlementFormQuery, SettlementFormResponse>
{
    public async Task<Result<SettlementFormResponse>> Handle(GetSettlementFormQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actor)
            return Result.Failure<SettlementFormResponse>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        var source = await sessions.GetSettlementSourceAsync(request.SessionId, false, ct);
        if (source.Session is not { } session)
            return Result.Failure<SettlementFormResponse>(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));
        if (!await sessions.CanManageFinanceAsync(session.GroupId, actor, ct))
            return Result.Failure<SettlementFormResponse>(Error.Forbidden("Settlement.Forbidden", "Bạn không có quyền quản lý tài chính nhóm."));
        var rule = source.Rule;

        var participants = new List<SettlementParticipantResponse>();
        foreach (var booking in session.Bookings.Where(b => !b.IsDeleted && b.Status == BookingStatus.CHECKED_IN))
        {
            var users = source.Users.Where(u => u.Id == booking.UserId && !u.IsDeleted).ToArray();
            var members = source.Memberships.Where(m => m.GroupId == session.GroupId && m.UserId == booking.UserId &&
                !m.IsDeleted && m.Status == ClubMemberStatus.ACTIVE).ToArray();
            if (users.Length != 1 || members.Length > 1)
                return Result.Failure<SettlementFormResponse>(Error.Validation("Settlement.InvalidParticipant",
                    $"Thiếu hoặc trùng dữ liệu người chơi {booking.UserId}."));
            var user = users[0];
            var member = members.SingleOrDefault();
            participants.Add(new SettlementParticipantResponse(user.Id, user.FullName, user.Gender,
                member?.MemberType ?? "GUEST", member?.MonthlyExpiryDate));
        }
        return Result.Success(new SettlementFormResponse(session.Id, session.ShuttlecockUsed, session.ShuttlecockPrice ?? 0m,
            session.CourtRentalCost, session.ExtraExpense, rule is null ? null : new SettlementRuleSnapshot(rule.Id,
                rule.FeeMode, rule.DailyMemberFee, rule.GuestFee, rule.FemaleDiscountDaily, rule.FemaleDiscountGuest,
                rule.RoundRule, rule.AppliedAt), participants,
            session.Bookings.Count(b => !b.IsDeleted && b.Status == BookingStatus.CONFIRMED)));
    }
}
