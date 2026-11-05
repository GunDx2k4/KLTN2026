namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Notifications;

public sealed record LedgerUpdatedPush(Guid EventId, Guid GroupId, Guid LedgerId, Guid RecipientId);
