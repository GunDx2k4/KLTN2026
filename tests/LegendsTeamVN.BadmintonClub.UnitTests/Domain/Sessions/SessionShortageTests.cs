using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.UnitTests.Domain.Sessions;

public sealed class SessionShortageTests
{
    private static readonly DateTime Start = new(2026, 11, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void UnansweredReminderKeepsSessionOpenUntilEndAndThenClosesOnce()
    {
        var session = NewSession();
        Assert.Equal(SessionShortageNotice.Initial, session.ProcessShortage(Start.AddHours(-2)));
        Assert.Null(session.ProcessShortage(Start.AddHours(-2)));
        Assert.Equal(SessionShortageNotice.Final, session.ProcessShortage(Start.AddMinutes(-30)));

        Assert.Null(session.ProcessShortage(Start));
        Assert.Equal(SessionStatus.OPEN, session.Status);
        Assert.Equal(SessionShortageNotice.NotHeld, session.ProcessShortage(session.EndTime));
        Assert.Equal(SessionStatus.NOT_HELD, session.Status);
        Assert.Equal(session.EndTime, session.NotHeldAt);
        Assert.Equal(200_000m, session.CourtRentalCost);
        Assert.Empty(session.Fees);
        Assert.Null(session.ProcessShortage(session.EndTime.AddMinutes(1)));
        Assert.True(session.ValidateSettlement(session.EndTime).IsFailure);
    }

    [Fact]
    public void ExtraReminderCanBeRescheduledOnceBeforeFinalReminder()
    {
        var session = NewSession();
        var first = Start.AddHours(-2);
        session.ProcessShortage(first);
        Assert.True(session.SetShortageReminder(15, first).IsSuccess);
        Assert.True(session.SetShortageReminder(30, first).IsSuccess);
        Assert.Null(session.ProcessShortage(first.AddMinutes(15)));
        Assert.Equal(SessionShortageNotice.Reminder, session.ProcessShortage(first.AddMinutes(30)));
        Assert.True(session.SetShortageReminder(15, first.AddMinutes(32)).IsFailure);
        Assert.Equal(SessionShortageNotice.Final, session.ProcessShortage(Start.AddMinutes(-30)));
    }

    [Fact]
    public void DelayedPollMergesWarningsAndKeepsExistingFeesAndLegacySessions()
    {
        var session = NewSession();
        Assert.Equal(SessionShortageNotice.Final, session.ProcessShortage(Start.AddMinutes(-15)));
        Assert.Null(session.ProcessShortage(session.EndTime, hasStoredFees: true));
        Assert.Equal(SessionStatus.OPEN, session.Status);

        var legacy = new BadmintonSession(Guid.NewGuid(), "Buổi cũ", Start, Start.AddHours(2), courtLocation: "Sân");
        Assert.Null(legacy.ProcessShortage(legacy.EndTime));
        Assert.Equal(SessionStatus.OPEN, legacy.Status);
    }

    private static BadmintonSession NewSession()
    {
        var session = new BadmintonSession(Guid.NewGuid(), "Buổi thử", Start, Start.AddHours(2), courtLocation: "Sân",
            courtRentalCost: 200_000m);
        Assert.True(session.ConfigureShortage(4, 120, session.MaxPlayers, false).IsSuccess);
        session.ScheduleShortageCheck();
        return session;
    }
}
