using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.UnitTests.Domain.Sessions;

[Trait("Category", "Domain")]
[Trait("FR", "FR08")]
[Trait("FR", "FR10")]
public sealed class SessionBookingTests
{
    [Fact]
    public void SessionBooking_QueueAndCheckIn_ShouldWorkAsExpected()
    {
        var sessionId = Guid.NewGuid();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        // User 1: Confirmed
        var booking1 = new SessionBooking(sessionId, user1, status: BookingStatus.CONFIRMED, queueOrder: 0);
        booking1.Status.Should().Be(BookingStatus.CONFIRMED);
        booking1.QueueOrder.Should().Be(0);

        // User 2: Waiting in queue (Order 1)
        var booking2 = new SessionBooking(sessionId, user2, status: BookingStatus.WAITING, queueOrder: 1);
        booking2.Status.Should().Be(BookingStatus.WAITING);
        booking2.QueueOrder.Should().Be(1);

        // Auto-promote booking 2 when booking 1 is absent
        booking1.UpdateStatus(BookingStatus.ABSENT);
        booking2.UpdateStatus(BookingStatus.CONFIRMED);
        booking2.UpdateQueueOrder(0);

        booking1.Status.Should().Be(BookingStatus.ABSENT);
        booking2.Status.Should().Be(BookingStatus.CONFIRMED);
        booking2.QueueOrder.Should().Be(0);

        // Check in
        booking2.CheckIn();
        booking2.Status.Should().Be(BookingStatus.CHECKED_IN);
        booking2.CheckinAt.Should().NotBeNull();
    }
}
