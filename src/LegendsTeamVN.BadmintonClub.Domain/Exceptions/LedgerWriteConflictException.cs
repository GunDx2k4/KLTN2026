namespace LegendsTeamVN.BadmintonClub.Domain.Exceptions;

public sealed class LedgerWriteConflictException(string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}
