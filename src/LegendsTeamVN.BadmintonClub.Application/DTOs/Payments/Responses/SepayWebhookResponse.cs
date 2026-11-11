using System.Text.Json.Serialization;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;

public sealed record SepayWebhookResponse([property: JsonPropertyName("success")] bool Success);
