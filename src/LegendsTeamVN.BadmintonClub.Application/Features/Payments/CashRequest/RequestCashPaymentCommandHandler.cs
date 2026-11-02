using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.CashRequest;

public sealed class RequestCashPaymentCommandHandler(IPaymentRepository fees, ICurrentUserService currentUser,
    IOptionsMonitor<PaymentsOptions> options, TimeProvider clock) : ICommandHandler<RequestCashPaymentCommand, PersonalFeeResponse>
{
    public async Task<Result<PersonalFeeResponse>> Handle(RequestCashPaymentCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid user) return Result.Failure<PersonalFeeResponse>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        var flags = options.CurrentValue;
        if (!flags.Enabled || !flags.WritesEnabled) return Result.Failure<PersonalFeeResponse>(Error.Conflict("Payments.WritesDisabled", "Chức năng thanh toán đang tạm ngừng."));
        var fee = await fees.GetMineAsync(request.FeeId, user, true, ct);
        if (fee is null) return Result.Failure<PersonalFeeResponse>(Error.NotFound("Payments.NotFound", "Không tìm thấy khoản thu."));
        var valid = fee.ValidatePaymentRequest(PaymentMethod.CASH);
        if (valid.IsFailure) return Result.Failure<PersonalFeeResponse>(valid.Error);
        if (fee.Session.IsDeleted || fee.Session.Group.IsDeleted || !fee.Session.Group.IsActive || !fee.Session.PublishedPreviewId.HasValue)
            return Result.Failure<PersonalFeeResponse>(Error.Conflict("Payments.SessionUnavailable", "Buổi chơi chưa sẵn sàng thanh toán."));
        if (!fee.PaymentRequestId.HasValue && (await fees.CashRecipientsAsync(fee.SessionId, ct)).Count == 0)
            return Result.Failure<PersonalFeeResponse>(Error.Conflict("Payments.NoCashRecipient", "Nhóm chưa có người quản lý thu tiền."));
        var result = fee.RequestCash(clock.GetUtcNow().UtcDateTime);
        return result.IsFailure ? Result.Failure<PersonalFeeResponse>(result.Error) : Result.Success(PersonalFeeMapper.Map(fee, flags.Enabled, flags.WritesEnabled));
    }
}
