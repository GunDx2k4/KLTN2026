using LegendsTeamVN.BadmintonClub.Domain.Services;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Settlement.Responses;

public sealed record SettlementPreviewResponse(SettlementAllocation Allocation, string PreviewToken, DateTime ExpiresAt);
