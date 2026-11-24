using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.UpdateMemberFeePlan;

public sealed class UpdateMemberFeePlanCommandHandler(ILedgerWriteRepository repository,
    IGroupAuthorizationService authorization, ICurrentUserService currentUser)
    : ICommandHandler<UpdateMemberFeePlanCommand, bool>
{
    public async Task<Result<bool>> Handle(UpdateMemberFeePlanCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actor)
            return Result.Failure<bool>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        if (!await authorization.HasPermissionAsync(actor, request.ClubId, GroupPermissions.Member.RoleManage, ct))
            return Result.Failure<bool>(Error.Forbidden("Club.Forbidden", "Bạn không có quyền quản lý thành viên."));
        if (request.MemberType is not ("MONTHLY" or "DAILY"))
            return Result.Failure<bool>(Error.Validation("Club.InvalidMemberType", "Gói hội viên không hợp lệ."));
        if (!await repository.LockGroupAsync(request.ClubId, ct))
            return Result.Failure<bool>(Error.NotFound("Club.NotFound", "Không tìm thấy câu lạc bộ."));
        var member = await repository.LockMemberAsync(request.ClubId, request.UserId, ct);
        if (member is null || member.IsDeleted || member.Status != ClubMemberStatus.ACTIVE)
            return Result.Failure<bool>(Error.NotFound("ClubMember.NotFound", "Không tìm thấy thành viên đang hoạt động."));
        if (member.MemberType == "GUEST")
            return Result.Failure<bool>(Error.Conflict("Club.GuestFeePlan", "Không thể đổi gói của khách vãng lai."));
        if (member.MemberType != request.MemberType &&
            await repository.HasUnpaidContributionAsync(request.ClubId, request.UserId, ct))
            return Result.Failure<bool>(Error.Conflict("Club.UnpaidMonthlyContribution", "Cần xử lý khoản phí tháng chưa thanh toán trước khi đổi gói."));
        member.UpdateMemberType(request.MemberType, member.MonthlyExpiryDate);
        return Result.Success(true);
    }
}
