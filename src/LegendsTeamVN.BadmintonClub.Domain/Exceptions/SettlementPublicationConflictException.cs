namespace LegendsTeamVN.BadmintonClub.Domain.Exceptions;

public sealed class SettlementPublicationConflictException(string code, string message, Exception innerException)
    : InvalidOperationException(message, innerException)
{
    public string Code { get; } = code;
}
