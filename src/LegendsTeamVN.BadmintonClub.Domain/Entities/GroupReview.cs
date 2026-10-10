using LegendsTeamVN.Core.Domain.Aggregates;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class GroupReview : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public Guid GroupId { get; private set; }
    public int Rating { get; private set; }
    public string? Criteria { get; private set; }
    public string? Comment { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public virtual AppUser User { get; private set; } = default!;
    public virtual ClubGroup Group { get; private set; } = default!;

    protected GroupReview() { }

    public GroupReview(
        Guid userId,
        Guid groupId,
        int rating,
        string? criteria = null,
        string? comment = null)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        GroupId = groupId;
        Rating = rating;
        Criteria = criteria;
        Comment = comment;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateReview(int rating, string? criteria, string? comment)
    {
        Rating = rating;
        Criteria = criteria;
        Comment = comment;
    }
}
