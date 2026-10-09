using LegendsTeamVN.Core.Domain.Aggregates;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class ClubSession : AggregateRoot<Guid>
{
    public Guid ClubId { get; private set; }
    public string Title { get; private set; } = default!;
    public DateTime StartTime { get; private set; }
    public DateTime EndTime { get; private set; }
    public string? Location { get; private set; }
    public decimal FeePerMember { get; private set; }
    public int MaxMembers { get; private set; }

    public virtual Club Club { get; private set; } = default!;

    protected ClubSession() { }

    public ClubSession(Guid clubId, string title, DateTime startTime, DateTime endTime, string? location, decimal feePerMember = 0, int maxMembers = 12)
    {
        Id = Guid.NewGuid();
        ClubId = clubId;
        Title = title;
        StartTime = startTime;
        EndTime = endTime;
        Location = location;
        FeePerMember = feePerMember;
        MaxMembers = maxMembers;
    }
}
