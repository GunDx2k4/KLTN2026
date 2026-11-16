using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;

public sealed record LedgerCapabilitiesResponse(bool CanView, bool CanManage, bool CanWrite);
