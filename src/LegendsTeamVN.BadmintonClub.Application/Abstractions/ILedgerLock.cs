namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;

public interface ILedgerLock
{
    Task<IAsyncDisposable?> AcquireAsync(Guid groupId, CancellationToken ct);
}
