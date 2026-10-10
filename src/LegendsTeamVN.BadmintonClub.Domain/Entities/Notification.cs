using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Domain.Aggregates;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class Notification : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = default!;
    public string Content { get; private set; } = default!;

    /// <summary>
    /// Loại thông báo của thông báo. Miền giá trị: SESSION, QUEUE, PAYMENT, MATCH, GUEST_RECRUIT.
    /// </summary>
    public NotificationType Type { get; private set; }

    public string? ReferenceType { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public virtual AppUser User { get; private set; } = default!;

    protected Notification() { }

    public Notification(
        Guid userId,
        string title,
        string content,
        NotificationType type,
        string? referenceType = null,
        Guid? referenceId = null)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Title = title;
        Content = content;
        Type = type;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        IsRead = false;
        CreatedAt = DateTime.UtcNow;
    }

    public Notification(
        Guid userId,
        string title,
        string content,
        string type,
        string? referenceType = null,
        Guid? referenceId = null)
        : this(userId, title, content, ParseType(type), referenceType, referenceId)
    {
    }

    public void MarkAsRead()
    {
        IsRead = true;
    }

    private static NotificationType ParseType(string type)
    {
        return Enum.TryParse<NotificationType>(type, true, out var result) ? result : NotificationType.SESSION;
    }
}
