using LegendsTeamVN.Core.Domain.Aggregates;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class Club : AggregateRoot<Guid>
{
    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public string? Description { get; private set; }
    public string? AvatarUrl { get; private set; }
    public bool IsActive { get; private set; }

    public virtual ICollection<ClubMember> Members { get; private set; } = new List<ClubMember>();
    public virtual ICollection<ClubSession> Sessions { get; private set; } = new List<ClubSession>();

    protected Club() { }

    public Club(string name, string code, string? description = null, string? avatarUrl = null)
    {
        Id = Guid.NewGuid();
        Name = name;
        Code = code;
        Description = description;
        AvatarUrl = avatarUrl;
        IsActive = true;
    }

    public void UpdateInfo(string name, string? description, string? avatarUrl)
    {
        Name = name;
        Description = description;
        AvatarUrl = avatarUrl;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }
}
