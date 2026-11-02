using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.TransferNotice;

public sealed class ReportBankTransferCommandHandler(IPaymentRepository fees, ICurrentUserService currentUser,
    IOptionsMonitor<PaymentsOptions> options, TimeProvider clock) : ICommandHandler<ReportBankTransferCommand, PersonalFeeResponse>
{
    public async Task<Result<PersonalFeeResponse>> Handle(ReportBankTransferCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid user) return Result.Failure<PersonalFeeResponse>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        var flags = options.CurrentValue;
        if (!flags.Enabled || !flags.WritesEnabled) return Result.Failure<PersonalFeeResponse>(Error.Conflict("Payments.WritesDisabled", "Chức năng thanh toán đang tạm ngừng."));
        var fee = await fees.GetMineAsync(request.FeeId, user, true, ct);
        if (fee is null) return Result.Failure<PersonalFeeResponse>(Error.NotFound("Payments.NotFound", "Không tìm thấy khoản thu."));
        if (fee.Session.IsDeleted || fee.Session.Group.IsDeleted || !fee.Session.Group.IsActive || !fee.Session.PublishedPreviewId.HasValue)
            return Result.Failure<PersonalFeeResponse>(Error.Conflict("Payments.SessionUnavailable", "Buổi chơi chưa sẵn sàng thanh toán."));
        var result = fee.ReportTransfer(clock.GetUtcNow().UtcDateTime);
        return result.IsFailure ? Result.Failure<PersonalFeeResponse>(result.Error) : Result.Success(PersonalFeeMapper.Map(fee, flags.Enabled, flags.WritesEnabled));
    }
}
