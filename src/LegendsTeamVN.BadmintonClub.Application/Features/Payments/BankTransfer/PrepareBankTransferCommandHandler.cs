using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Domain.Entities;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.BankTransfer;

public sealed class PrepareBankTransferCommandHandler(IPaymentRepository fees, ICurrentUserService currentUser,
    IOptionsMonitor<PaymentsOptions> options, IVietQrPayloadGenerator generator, TimeProvider clock) : ICommandHandler<PrepareBankTransferCommand, PersonalFeeResponse>
{
    public async Task<Result<PersonalFeeResponse>> Handle(PrepareBankTransferCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid user) return Result.Failure<PersonalFeeResponse>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        var flags = options.CurrentValue;
        if (!flags.Enabled || !flags.WritesEnabled) return Result.Failure<PersonalFeeResponse>(Error.Conflict("Payments.WritesDisabled", "Chức năng thanh toán đang tạm ngừng."));
        var fee = await fees.GetMineAsync(request.FeeId, user, true, ct);
        if (fee is null) return Result.Failure<PersonalFeeResponse>(Error.NotFound("Payments.NotFound", "Không tìm thấy khoản thu."));
        if (fee.Session.IsDeleted || fee.Session.Group.IsDeleted || !fee.Session.Group.IsActive || !fee.Session.PublishedPreviewId.HasValue)
            return Result.Failure<PersonalFeeResponse>(Error.Conflict("Payments.SessionUnavailable", "Buổi chơi chưa sẵn sàng thanh toán."));
        var valid = fee.ValidateBankTransferPreparation();
        if (valid.IsFailure) return Result.Failure<PersonalFeeResponse>(valid.Error);
        if (fee.PaymentRequestId.HasValue)
        {
            var access = fee.ValidateQrAccess();
            return access.IsFailure ? Result.Failure<PersonalFeeResponse>(access.Error) : Result.Success(PersonalFeeMapper.Map(fee, flags.Enabled, flags.WritesEnabled));
        }
        var banks = await fees.GetActiveBanksAsync(fee.Session.GroupId, true, ct);
        if (banks.Count != 1) return Result.Failure<PersonalFeeResponse>(Error.Conflict("Payments.BankUnavailable", "Nhóm cần đúng một tài khoản ngân hàng hoạt động."));
        var bank = banks[0];
        var snapshot = new PaymentBankSnapshot(bank.Id, bank.BankBin!, bank.BankName, bank.AccountNumber, bank.AccountHolder);
        var payload = generator.Generate(snapshot, fee.Amount, fee.PaymentCode);
        if (payload.IsFailure) return Result.Failure<PersonalFeeResponse>(payload.Error);
        var result = fee.PrepareBankTransfer(snapshot, payload.Value, clock.GetUtcNow().UtcDateTime);
        return result.IsFailure ? Result.Failure<PersonalFeeResponse>(result.Error) : Result.Success(PersonalFeeMapper.Map(fee, flags.Enabled, flags.WritesEnabled));
    }
}
