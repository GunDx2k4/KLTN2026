namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;

public interface ISessionShortageRepository
{
    Task<IReadOnlyList<Guid>> FindDueAsync(DateTime now, int limit, CancellationToken ct);
    Task<IReadOnlyList<Guid>> FindHostsAsync(Guid groupId, CancellationToken ct);
    Task<bool> CanDeliverAsync(Guid sessionId, Guid groupId, bool closed, DateTime now, CancellationToken ct);
    Task<bool> HasFeesAsync(Guid sessionId, CancellationToken ct);
}
