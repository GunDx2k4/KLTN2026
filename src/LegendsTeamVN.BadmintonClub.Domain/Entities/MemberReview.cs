using LegendsTeamVN.Core.Domain.Aggregates;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class MemberReview : AggregateRoot<Guid>
{
    public Guid ReviewerId { get; private set; }
    public Guid RevieweeId { get; private set; }
    public int Rating { get; private set; }
    public string? Criteria { get; private set; }
    public string? Comment { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public virtual AppUser Reviewer { get; private set; } = default!;
    public virtual AppUser Reviewee { get; private set; } = default!;

    protected MemberReview() { }

    public MemberReview(
        Guid reviewerId,
        Guid revieweeId,
        int rating,
        string? criteria = null,
        string? comment = null)
    {
        Id = Guid.NewGuid();
        ReviewerId = reviewerId;
        RevieweeId = revieweeId;
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
