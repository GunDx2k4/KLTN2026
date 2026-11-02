namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;

public sealed record PaymentBankAccountResponse(Guid Id, string? BankBin, string BankName, string AccountNumber, string AccountHolder);
