using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.GetMine;

public sealed class GetMyFeeQrImageQueryHandler(IPaymentRepository fees, ICurrentUserService currentUser,
    IOptionsMonitor<PaymentsOptions> options, IVietQrImageRenderer renderer) : IQueryHandler<GetMyFeeQrImageQuery, byte[]>
{
    public async Task<Result<byte[]>> Handle(GetMyFeeQrImageQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid user) return Result.Failure<byte[]>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        var fee = await fees.GetMineAsync(request.FeeId, user, false, ct);
        if (fee is null) return Result.Failure<byte[]>(Error.NotFound("Payments.NotFound", "Không tìm thấy khoản thu."));
        if (!options.CurrentValue.Enabled) return Result.Failure<byte[]>(Error.Conflict("Payments.Disabled", "Chức năng thanh toán đang tạm ngừng."));
        if (fee.Session.IsDeleted || fee.Session.Group.IsDeleted || !fee.Session.Group.IsActive)
            return Result.Failure<byte[]>(Error.Conflict("Payments.SessionUnavailable", "Buổi chơi không sẵn sàng thanh toán."));
        var access = fee.ValidateQrAccess();
        return access.IsFailure ? Result.Failure<byte[]>(access.Error) : Result.Success(renderer.RenderPng(fee.QrCodePayload));
    }
}
