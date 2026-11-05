using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger;

public sealed class LedgerWriteSupport(ILedgerWriteRepository writes, ILedgerReadRepository reads,
    ICurrentUserService currentUser, IOptionsMonitor<LedgerOptions> flags)
{
    public async Task<Result<Guid>> BeginAsync(Guid groupId, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actor)
            return Result.Failure<Guid>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        if (!await reads.HasPermissionAsync(groupId, actor, GroupPermissions.Finance.LedgerManage, ct))
            return Result.Failure<Guid>(Error.Forbidden("Ledger.Forbidden", "Bạn không có quyền quản trị sổ quỹ của nhóm."));
        if (!await writes.LockGroupAsync(groupId, ct))
            return Result.Failure<Guid>(Error.NotFound("Ledger.GroupNotFound", "Không tìm thấy nhóm."));
        if (!flags.CurrentValue.Enabled || !flags.CurrentValue.WritesEnabled)
            return Result.Failure<Guid>(Error.Failure("Ledger.WritesDisabled", "Ghi sổ quỹ đang tạm dừng."));
        if (!await reads.HasPermissionAsync(groupId, actor, GroupPermissions.Finance.LedgerManage, ct))
            return Result.Failure<Guid>(Error.Forbidden("Ledger.Forbidden", "Quyền quản trị sổ quỹ đã thay đổi."));
        return Result.Success(actor);
    }

    public static Result<LedgerWriteResponse> Conflict() => Result.Failure<LedgerWriteResponse>(
        Error.Conflict("Ledger.IdempotencyConflict", "Mã yêu cầu đã được dùng với nội dung khác."));
}
