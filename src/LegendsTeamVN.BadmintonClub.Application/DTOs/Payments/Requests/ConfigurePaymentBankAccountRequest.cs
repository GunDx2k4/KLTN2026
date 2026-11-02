namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Requests;

public sealed record ConfigurePaymentBankAccountRequest(string BankBin, string BankName, string AccountNumber, string AccountHolder);
