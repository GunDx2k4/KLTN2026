namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;

public interface IReconciliationLock
{
    Task<IAsyncDisposable?> AcquireAsync(long providerTransactionId, CancellationToken ct);
}
