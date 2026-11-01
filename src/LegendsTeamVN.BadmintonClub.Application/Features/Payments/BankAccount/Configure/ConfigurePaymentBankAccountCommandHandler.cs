using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Domain.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.BankAccount.Configure;

public sealed class ConfigurePaymentBankAccountCommandHandler(IPaymentRepository fees, ICurrentUserService currentUser,
    IOptionsMonitor<PaymentsOptions> options, IUnitOfWork unitOfWork) : ICommandHandler<ConfigurePaymentBankAccountCommand, PaymentBankAccountResponse>
{
    public async Task<Result<PaymentBankAccountResponse>> Handle(ConfigurePaymentBankAccountCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid user) return Result.Failure<PaymentBankAccountResponse>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        var flags = options.CurrentValue;
        if (!flags.Enabled || !flags.WritesEnabled) return Result.Failure<PaymentBankAccountResponse>(Error.Conflict("Payments.WritesDisabled", "Chức năng thanh toán đang tạm ngừng."));
        if (!await fees.CanManageBankAsync(request.ClubId, user, ct)) return Result.Failure<PaymentBankAccountResponse>(Error.Forbidden("Payments.BankForbidden", "Bạn không có quyền cấu hình ngân hàng nhóm."));
        var banks = await fees.GetActiveBanksAsync(request.ClubId, true, ct);
        if (!await fees.CanManageBankAsync(request.ClubId, user, ct)) return Result.Failure<PaymentBankAccountResponse>(Error.Forbidden("Payments.BankForbidden", "Quyền cấu hình ngân hàng đã thay đổi."));
        if (banks.Count > 1) return Result.Failure<PaymentBankAccountResponse>(Error.Conflict("Payments.BankAmbiguous", "Nhóm có nhiều tài khoản hoạt động. Cần kiểm tra dữ liệu trước."));
        var input = request.Input;
        var snapshot = new PaymentBankSnapshot(Guid.NewGuid(), input.BankBin, input.BankName, input.AccountNumber, input.AccountHolder);
        var bank = banks.SingleOrDefault();
        if (bank is null || !bank.Matches(snapshot))
        {
            if (bank is not null)
            {
                bank.SetActive(false);
                await unitOfWork.SaveChangesAsync(ct);
            }
            bank = GroupBankAccount.CreatePaymentAccount(request.ClubId, snapshot);
            fees.AddBank(bank);
        }
        return Result.Success(new PaymentBankAccountResponse(bank.Id, bank.BankBin, bank.BankName, bank.AccountNumber, bank.AccountHolder));
    }
}
