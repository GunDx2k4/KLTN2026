using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.BankAccount.Get;

public sealed class GetPaymentBankAccountQueryHandler(IPaymentRepository fees, ICurrentUserService currentUser)
    : IQueryHandler<GetPaymentBankAccountQuery, PaymentBankAccountResponse>
{
    public async Task<Result<PaymentBankAccountResponse>> Handle(GetPaymentBankAccountQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid user) return Result.Failure<PaymentBankAccountResponse>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        var banks = await fees.GetActiveBanksAsync(request.ClubId, false, ct);
        if (banks.Count != 1) return Result.Failure<PaymentBankAccountResponse>(Error.Conflict("Payments.BankUnavailable", "Nhóm chưa có tài khoản ngân hàng hoạt động."));
        var bank = banks[0];
        return Result.Success(new PaymentBankAccountResponse(bank.Id, bank.BankBin, bank.BankName, bank.AccountNumber, bank.AccountHolder));
    }
}
