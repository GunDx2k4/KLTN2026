namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;

public sealed record CalculateSettlementRequest(
    int ShuttlecockUsed,
    decimal ShuttlecockPrice,
    decimal ExtraFee,
    decimal? CourtFee = null
);
