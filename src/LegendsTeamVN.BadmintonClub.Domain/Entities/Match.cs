using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Domain.Aggregates;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class Match : AggregateRoot<Guid>
{
    public Guid BookingDetailId { get; private set; }
    public Guid HostId { get; private set; }
    public int MaxPlayers { get; private set; }
    public DateTimeOffset RegistrationClosesAt { get; private set; }
    public long AttendanceVersion { get; private set; }
    public decimal PricePerPlayer { get; private set; }
    public MatchStatus Status { get; private set; }
    public string? Description { get; private set; }

    public virtual BookingDetail BookingDetail { get; private set; } = default!;
    public virtual ICollection<MatchPlayer> MatchPlayers { get; private set; } = new List<MatchPlayer>();

    protected Match() { }

    public Match(Guid bookingDetailId, Guid hostId, int maxPlayers, decimal pricePerPlayer, DateTimeOffset registrationClosesAt, string? description = null)
    {
        if (maxPlayers <= 0) throw new ArgumentOutOfRangeException(nameof(maxPlayers));
        if (registrationClosesAt == default) throw new ArgumentOutOfRangeException(nameof(registrationClosesAt));
        Id = Guid.NewGuid();
        BookingDetailId = bookingDetailId;
        HostId = hostId;
        MaxPlayers = maxPlayers;
        RegistrationClosesAt = registrationClosesAt.ToUniversalTime();
        PricePerPlayer = pricePerPlayer;
        Status = MatchStatus.Open;
        Description = description;
    }

    public void UpdateMatch(int maxPlayers, decimal pricePerPlayer, string? description)
    {
        MaxPlayers = maxPlayers;
        PricePerPlayer = pricePerPlayer;
        Description = description;
    }

    public void UpdateStatus(MatchStatus status)
    {
        Status = status;
    }

    public void RecordAttendanceChange(int confirmedPlayers)
    {
        if (confirmedPlayers < 0) throw new ArgumentOutOfRangeException(nameof(confirmedPlayers));
        Status = confirmedPlayers >= MaxPlayers ? MatchStatus.Full : MatchStatus.Open;
        AttendanceVersion++;
    }
}
