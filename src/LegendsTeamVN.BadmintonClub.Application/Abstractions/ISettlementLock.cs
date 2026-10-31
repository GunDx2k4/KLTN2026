namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;

public interface ISettlementLock
{
    Task<IAsyncDisposable?> AcquireAsync(Guid sessionId, CancellationToken cancellationToken);
}
