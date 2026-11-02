using LegendsTeamVN.BadmintonClub.Application.Features.Recruitments;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.UnitTests;

public sealed class RecruitmentCapacityTests
{
    private static readonly DateTime Now = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CheckInDoesNotCreateAnotherRecruitmentSlot()
    {
        var s = Session(); var r = new GuestRecruitment(s.Id, 2); s.Recruitments.Add(r);
        var b = new SessionBooking(s.Id, Guid.NewGuid(), r.Id); s.Bookings.Add(b);
        Assert.Equal(1, RecruitmentRules.RemainingSlots(s, r));
        b.MarkPresent(Guid.NewGuid(), Now);
        Assert.Equal(1, RecruitmentRules.RemainingSlots(s, r));
    }

    [Fact]
    public void NoShowReopensFilledRecruitmentAndExplicitClosureIsPreserved()
    {
        var s = Session(); var r = new GuestRecruitment(s.Id, 1); s.Recruitments.Add(r);
        var b = new SessionBooking(s.Id, Guid.NewGuid(), r.Id); s.Bookings.Add(b);
        RecruitmentRules.Synchronize(s, Now);
        Assert.Equal(RecruitmentStatus.FILLED, r.Status);
        b.MarkNoShow(Guid.NewGuid(), Now);
        RecruitmentRules.Synchronize(s, Now);
        Assert.Equal(RecruitmentStatus.ACTIVE, r.Status);
        r.UpdateStatus(RecruitmentStatus.EXPIRED);
        RecruitmentRules.Synchronize(s, Now);
        Assert.Equal(RecruitmentStatus.EXPIRED, r.Status);
    }

    [Fact]
    public void GuestSlotsAreLimitedBySessionCapacityAndExistingWaiters()
    {
        var s = Session(); var r = new GuestRecruitment(s.Id, 10); s.Recruitments.Add(r);
        s.Bookings.Add(new SessionBooking(s.Id, Guid.NewGuid()));
        Assert.Equal(3, RecruitmentRules.RemainingSlots(s, r));
        s.Bookings.Add(new SessionBooking(s.Id, Guid.NewGuid(), status: BookingStatus.WAITING, queueOrder: 1));
        Assert.Equal(0, RecruitmentRules.RemainingSlots(s, r));
        RecruitmentRules.Synchronize(s, Now);
        Assert.Equal(RecruitmentStatus.FILLED, r.Status);
    }

    [Fact]
    public void ClosingOrEndingSessionExpiresRecruitment()
    {
        var s = Session(); var r = new GuestRecruitment(s.Id, 1); s.Recruitments.Add(r);
        RecruitmentRules.Synchronize(s, s.EndTime);
        Assert.Equal(RecruitmentStatus.EXPIRED, r.Status);
        var closed = Session(SessionStatus.COMPLETED);
        var second = new GuestRecruitment(closed.Id, 1); closed.Recruitments.Add(second);
        RecruitmentRules.Synchronize(closed, Now);
        Assert.Equal(RecruitmentStatus.EXPIRED, second.Status);
    }

    private static BadmintonSession Session(SessionStatus status = SessionStatus.OPEN) => new(Guid.NewGuid(), "Quota", Now.AddMinutes(30), Now.AddHours(2), "Court", maxPlayers: 4, status: status);
}
