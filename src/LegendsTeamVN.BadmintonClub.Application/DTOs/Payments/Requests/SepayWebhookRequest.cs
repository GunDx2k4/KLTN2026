using System.Text.Json.Serialization;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Requests;

public sealed record SepayWebhookRequest(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("gateway")] string Gateway,
    [property: JsonPropertyName("transactionDate")] string TransactionDate,
    [property: JsonPropertyName("accountNumber")] string AccountNumber,
    [property: JsonPropertyName("subAccount")] string? SubAccount,
    [property: JsonPropertyName("transferType")] string TransferType,
    [property: JsonPropertyName("transferAmount")] decimal TransferAmount,
    [property: JsonPropertyName("referenceCode")] string? ReferenceCode,
    [property: JsonPropertyName("content")] string Content);
