using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger.GetAttachment;

public sealed class GetLedgerAttachmentQueryHandler(ILedgerReadRepository repository, ICurrentUserService currentUser,
    IOptionsMonitor<LedgerOptions> options) : IQueryHandler<GetLedgerAttachmentQuery, LedgerAttachmentResponse>
{
    public async Task<Result<LedgerAttachmentResponse>> Handle(GetLedgerAttachmentQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actor)
            return Result.Failure<LedgerAttachmentResponse>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        if (!options.CurrentValue.Enabled)
            return Result.Failure<LedgerAttachmentResponse>(Error.Failure("Ledger.Disabled", "Sổ quỹ đang tạm dừng."));
        if (!await repository.HasPermissionAsync(request.ClubId, actor, GroupPermissions.Finance.LedgerView, ct))
            return Result.Failure<LedgerAttachmentResponse>(Error.Forbidden("Ledger.Forbidden", "Bạn không có quyền xem sổ quỹ của nhóm."));
        var data = await repository.GetAttachmentAsync(request.ClubId, request.LedgerId, ct);
        return data is null
            ? Result.Failure<LedgerAttachmentResponse>(Error.NotFound("Ledger.AttachmentNotFound", "Không tìm thấy hóa đơn trong nhóm."))
            : Result.Success(data);
    }
}
