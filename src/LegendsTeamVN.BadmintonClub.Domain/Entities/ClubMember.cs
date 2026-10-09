using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Domain.Aggregates;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class ClubMember : AggregateRoot<Guid>
{
    public Guid ClubId { get; private set; }
    public Guid UserId { get; private set; }
    public ClubRole Role { get; private set; }
    public ClubMemberStatus Status { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public string? Nickname { get; private set; }

    public virtual Club Club { get; private set; } = default!;

    protected ClubMember() { }

    public ClubMember(Guid clubId, Guid userId, ClubRole role, ClubMemberStatus status = ClubMemberStatus.Active, string? nickname = null)
    {
        Id = Guid.NewGuid();
        ClubId = clubId;
        UserId = userId;
        Role = role;
        Status = status;
        JoinedAt = DateTime.UtcNow;
        Nickname = nickname;
    }

    public void UpdateRole(ClubRole newRole)
    {
        Role = newRole;
    }

    public void UpdateStatus(ClubMemberStatus newStatus)
    {
        Status = newStatus;
    }

    public void UpdateNickname(string? nickname)
    {
        Nickname = nickname;
    }
}
