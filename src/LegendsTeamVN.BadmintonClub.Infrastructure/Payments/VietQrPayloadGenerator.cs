using System.Globalization;
using System.Text.RegularExpressions;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.Core.Utilities.Results;
using Payment.QrPayload;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Payments;

public sealed class VietQrPayloadGenerator : IVietQrPayloadGenerator
{
    public Result<string> Generate(PaymentBankSnapshot bank, decimal amount, string paymentCode)
    {
        if (bank is null || !bank.IsValid || amount <= 0 || amount != decimal.Truncate(amount) || amount > 9999999999999m ||
            paymentCode is null || !Regex.IsMatch(paymentCode, @"\ACLB [0-9A-F]{12} [0-9A-F]{12}\z", RegexOptions.CultureInvariant))
            return Result.Failure<string>(Error.Conflict("Payments.InvalidQrInput", "Thông tin tạo mã thanh toán không hợp lệ."));
        var result = QrPayloadGenerator.GeneratePayload(bank.AccountNumber, bank.BankBin, paymentCode, amount.ToString("0", CultureInfo.InvariantCulture));
        return result.IsSuccess && !string.IsNullOrWhiteSpace(result.Content)
            ? Result.Success(result.Content)
            : Result.Failure<string>(Error.Conflict("Payments.QrGenerationFailed", "Không thể tạo mã thanh toán. Vui lòng thử lại."));
    }
}
