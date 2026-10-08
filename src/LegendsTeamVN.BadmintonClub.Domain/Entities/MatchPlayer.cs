using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Domain.Entities;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class MatchPlayer : SoftDeletableEntity<Guid>
{
    public Guid MatchId { get; private set; }
    public Guid UserId { get; private set; }
    public MatchPlayerStatus Status { get; private set; }
    public bool IsHost { get; private set; }
    public DateTimeOffset JoinedAt { get; private set; }

    public virtual Match Match { get; private set; } = default!;

    protected MatchPlayer() { }

    public MatchPlayer(Guid matchId, Guid userId, bool isHost = false, MatchPlayerStatus status = MatchPlayerStatus.Pending)
    {
        Id = Guid.NewGuid();
        MatchId = matchId;
        UserId = userId;
        IsHost = isHost;
        Status = status;
        JoinedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateStatus(MatchPlayerStatus status)
    {
        Status = status;
    }

    public void Register(MatchPlayerStatus status, DateTimeOffset joinedAt)
    {
        Status = status;
        JoinedAt = joinedAt;
        IsDeleted = false;
        DeletedOnUtc = null;
        DeletedBy = null;
    }
}
