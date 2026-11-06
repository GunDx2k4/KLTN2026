using LegendsTeamVN.Core.Domain.Aggregates;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class GroupReview : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid? SessionId { get; private set; }
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

    public bool MatchesSubmission(int rating, string? comment) => Rating == rating &&
        Comment == (string.IsNullOrWhiteSpace(comment) ? null : comment.Trim());

    public void UpdateReview(int rating, string? criteria, string? comment)
    {
        if (SessionId.HasValue) throw new InvalidOperationException("Đánh giá buổi đã gửi không được sửa.");
        Rating = rating;
        Criteria = criteria;
        Comment = comment;
    }

    public static GroupReview CreateForSession(BadmintonSession session, SessionBooking booking,
        int rating, string? comment, DateTime now)
    {
        Services.ReputationPolicy.RequireUtc(now);
        comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (rating is < 1 or > 5 || comment?.Length > 2000)
            throw new ArgumentException("Điểm sao hoặc phản hồi không hợp lệ.");
        if (session.IsDeleted || session.ReputationProcessingStatus != Services.ReputationProcessingStatus.Finalized
            || !session.FinancialReconciledAt.HasValue || now < session.FinancialReconciledAt.Value
            || now >= session.FinancialReconciledAt.Value.AddDays(7)
            || booking.SessionId != session.Id || !booking.ReputationReviewEligible)
            throw new InvalidOperationException("Không đủ điều kiện đánh giá buổi.");
        return new GroupReview(booking.UserId, session.GroupId, rating, comment: comment)
        {
            SessionId = session.Id,
            CreatedAt = new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc)
        };
    }
}
