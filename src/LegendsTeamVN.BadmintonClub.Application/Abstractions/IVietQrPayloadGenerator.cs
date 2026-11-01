using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;

public interface IVietQrPayloadGenerator
{
    Result<string> Generate(PaymentBankSnapshot bank, decimal amount, string paymentCode);
}
