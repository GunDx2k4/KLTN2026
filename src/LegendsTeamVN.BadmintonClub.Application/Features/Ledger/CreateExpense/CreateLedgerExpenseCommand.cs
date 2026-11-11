using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger.CreateExpense;

public sealed record CreateLedgerExpenseCommand(Guid GroupId, Guid RequestId, decimal Amount, LedgerCategory Category,
    string Description, Guid? SessionId = null, string? FileName = null, string? ContentType = null, byte[]? Content = null) : ILedgerCommand;
