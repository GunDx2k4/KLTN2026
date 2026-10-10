using LegendsTeamVN.Core.Domain.Entities;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class GroupSchedule : Entity<Guid>
{
    public Guid GroupId { get; private set; }
    public int DayOfWeek { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public bool IsActive { get; private set; }

    public virtual ClubGroup Group { get; private set; } = default!;

    protected GroupSchedule() { }

    public GroupSchedule(
        Guid groupId,
        int dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        bool isActive = true)
    {
        Id = Guid.NewGuid();
        GroupId = groupId;
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
        IsActive = isActive;
    }

    public void UpdateSchedule(int dayOfWeek, TimeOnly startTime, TimeOnly endTime)
    {
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }
}
