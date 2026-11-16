using LegendsTeamVN.BadmintonClub.Domain.Services;
namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
public sealed record ReputationPageResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
