using LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Settlement;
using LegendsTeamVN.BadmintonClub.Application.Tests.Common;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.BadmintonClub.Persistence;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Sessions;

public sealed class SettlementRotationSafetyTests : IDisposable
{
    private readonly BadmintonDbContext db = TestDbContextFactory.Create();
    private readonly ISessionRepository sessions = Substitute.For<ISessionRepository>();
    private readonly ICurrentUserService currentUser = Substitute.For<ICurrentUserService>();
    private readonly Guid actor = Guid.NewGuid();
    private readonly DateTime now = DateTime.UtcNow;

    public SettlementRotationSafetyTests()
    {
        currentUser.UserId.Returns(actor);
        sessions.CanManageFinanceAsync(Arg.Any<Guid>(), actor, Arg.Any<CancellationToken>()).Returns(true);
        sessions.When(r => r.AddSettlementFees(Arg.Any<IEnumerable<SessionFee>>()))
            .Do(call => new SessionRepository(db).AddSettlementFees(call.Arg<IEnumerable<SessionFee>>()));
    }

    private async Task<BadmintonSession> CreateSession(bool initialize = true)
    {
        var club = new ClubGroup("Settlement rotation safety");
        var session = new BadmintonSession(club.Id, "Session", now.AddHours(-1), now.AddHours(2), "Court", courtCount: 2);
        for (var i = 0; i < 12; i++)
        {
            var user = new AppUser(Guid.NewGuid(), "Player " + i);
            var booking = new SessionBooking(session.Id, user.Id);
            if (initialize) booking.MarkPresent(actor, now.AddMinutes(-10).AddSeconds(i));
            typeof(SessionBooking).GetProperty(nameof(SessionBooking.User))!.SetValue(booking, user);
            session.Bookings.Add(booking);
        }
        if (initialize)
        {
            Assert.True(session.CompleteAttendance(actor, now).IsSuccess);
            Assert.True(session.InitializeRotation(actor, now).IsSuccess);
        }
        db.ClubGroups.Add(club);
        db.BadmintonSessions.Add(session);
        await db.SaveChangesAsync();
        session.ClearDomainEvents();
        sessions.GetSettlementAsync(session.Id, true, Arg.Any<CancellationToken>()).Returns(session);
        return session;
    }

    private CalculateSessionSettlementCommandHandler Calculator() => new(sessions,
        new ClubRepository(db), new ClubMemberRepository(db), currentUser);

    private static SessionBooking FreePlayer(BadmintonSession session) => session.Bookings.First(b =>
        !session.Matches.Any(m => m.Players.Any(p => p.UserId == b.UserId)));

    [Fact]
    public async Task Calculate_WhenTargetIsPlaying_RejectsWithoutClosingOrChangingExpenses()
    {
        var session = await CreateSession();
        var player = FreePlayer(session);
        Assert.True(session.SetSelfRotationPause(player.UserId, true, session.RotationVersion, now).IsSuccess);
        var target = session.Matches.First(m => m.Status == MatchStatus.WAITING);
        Assert.True(session.StartRotationMatch(target.Id, now.AddSeconds(1)).IsSuccess);
        var version = session.RotationVersion;
        var nextCheck = session.NextProposalCheckAt;
        session.ClearDomainEvents();

        var result = await Calculator().Handle(new(session.Id, 5, 25000m, 10000m, 300000m), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Session.ActiveMatches", result.Error.Code);
        Assert.Equal(SessionStatus.PLAYING, session.Status);
        Assert.Equal(version, session.RotationVersion);
        Assert.Equal(nextCheck, session.NextProposalCheckAt);
        Assert.Equal(0, session.ShuttlecockUsed);
        Assert.Equal(0m, session.CourtRentalCost);
        Assert.Empty(session.Fees);
        Assert.Empty(session.DomainEvents);
        Assert.True(player.IsRotationPaused);
        Assert.Equal(target.Id, player.SelfPauseTargetMatchId);
        Assert.True(session.FinishRotationMatch(target.Id, now.AddMinutes(1)).IsSuccess);
        Assert.False(player.IsRotationPaused);
        await sessions.Received(1).GetSettlementAsync(session.Id, true, Arg.Any<CancellationToken>());
        sessions.DidNotReceive().Update(Arg.Any<BadmintonSession>());
        sessions.DidNotReceive().AddSettlementFees(Arg.Any<IEnumerable<SessionFee>>());
    }

    [Fact]
    public async Task Calculate_WhenAttendanceIsUnresolved_DoesNotSilentlyMarkNoShows()
    {
        var session = await CreateSession(false);
        var result = await Calculator().Handle(new(session.Id, 5, 25000m, 10000m, 300000m), default);

        Assert.True(result.IsFailure);
        Assert.Equal("Attendance.NoShowConfirmationRequired", result.Error.Code);
        Assert.Equal(SessionStatus.OPEN, session.Status);
        Assert.All(session.Bookings, b => Assert.Equal(BookingStatus.CONFIRMED, b.Status));
        Assert.Empty(session.Fees);
        Assert.Equal(0, session.ShuttlecockUsed);
        Assert.Empty(session.DomainEvents);
    }

    [Fact]
    public async Task Calculate_WhenSafeToClose_SkipsProposalsAndEmitsRotationChange()
    {
        var session = await CreateSession();
        var proposals = session.Matches.ToArray();
        var version = session.RotationVersion;

        var result = await Calculator().Handle(new(session.Id, 2, 25000m, 0m, 300000m), default);

        Assert.True(result.IsSuccess);
        Assert.Equal(SessionStatus.CALCULATED, session.Status);
        Assert.Null(session.NextProposalCheckAt);
        Assert.All(proposals, m => Assert.Equal(MatchStatus.SKIPPED, m.Status));
        Assert.True(session.RotationVersion > version);
        Assert.Single(session.DomainEvents.OfType<RotationChangedDomainEvent>());
        Assert.Equal("Closed", PersonalMatchSchedulePolicy.Evaluate(session,
            session.Bookings.First().UserId, now, true, true, true).Phase);
        Assert.Equal(12, session.Fees.Count);
        Assert.All(session.Fees, fee => Assert.Equal(EntityState.Added, db.Entry(fee).State));
        Assert.Equal(12, result.Value!.MemberFees.Count);
        await sessions.Received(1).GetSettlementAsync(session.Id, true, Arg.Any<CancellationToken>());
        await sessions.DidNotReceive().GetSettlementAsync(session.Id, false, Arg.Any<CancellationToken>());
        sessions.DidNotReceive().Update(Arg.Any<BadmintonSession>());

        await db.SaveChangesAsync();
        Assert.Equal(12, await db.SessionFees.CountAsync(f => f.SessionId == session.Id));
    }

    [Fact]
    public async Task MarkPaid_OnlyMarksFeeModified_AndPreservesRotationAndSelfPause()
    {
        var session = await CreateSession();
        var player = FreePlayer(session);
        Assert.True(session.SetSelfRotationPause(player.UserId, true, session.RotationVersion, now).IsSuccess);
        var target = session.Matches.First(m => m.Status == MatchStatus.WAITING);
        Assert.True(session.StartRotationMatch(target.Id, now.AddSeconds(1)).IsSuccess);
        var fee = new SessionFee(session.Id, player.UserId, 50000m, PriceType.GUEST, "TEST", "TEST");
        session.Fees.Add(fee);
        db.SessionFees.Add(fee);
        await db.SaveChangesAsync();
        var version = session.RotationVersion;
        var nextCheck = session.NextProposalCheckAt;
        session.ClearDomainEvents();
        var handler = new MarkMemberPaidCommandHandler(sessions, new ClubMemberRepository(db), currentUser);

        var result = await handler.Handle(new(session.Id, player.UserId), default);
        db.ChangeTracker.DetectChanges();

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.PAID, fee.PaymentStatus);
        Assert.Equal(EntityState.Modified, db.Entry(fee).State);
        Assert.Equal(EntityState.Unchanged, db.Entry(session).State);
        Assert.False(db.Entry(session).Property(s => s.RotationVersion).IsModified);
        Assert.False(db.Entry(session).Property(s => s.Status).IsModified);
        Assert.Equal(version, session.RotationVersion);
        Assert.Equal(nextCheck, session.NextProposalCheckAt);
        Assert.True(player.IsRotationPaused);
        Assert.Equal(target.Id, player.SelfPauseTargetMatchId);
        Assert.Equal(MatchStatus.PLAYING, target.Status);
        Assert.Empty(session.DomainEvents);
        Assert.Equal("PAID", result.Value!.MemberFees.Single(f => f.UserId == player.UserId).PaymentStatus);
        await sessions.Received(1).GetSettlementAsync(session.Id, true, Arg.Any<CancellationToken>());
        sessions.DidNotReceive().Update(Arg.Any<BadmintonSession>());
    }

    [Fact]
    public async Task GetSettlement_UsesReadOnlyRepositoryPath()
    {
        var session = await CreateSession();
        sessions.GetSettlementAsync(session.Id, false, Arg.Any<CancellationToken>()).Returns(session);
        sessions.ClearReceivedCalls();
        var handler = new GetSessionSettlementQueryHandler(sessions, new ClubMemberRepository(db));

        var result = await handler.Handle(new(session.Id), default);

        Assert.True(result.IsSuccess);
        await sessions.Received(1).GetSettlementAsync(session.Id, false, Arg.Any<CancellationToken>());
        await sessions.DidNotReceive().GetSettlementAsync(session.Id, true, Arg.Any<CancellationToken>());
        sessions.DidNotReceive().Update(Arg.Any<BadmintonSession>());
    }

    [Fact]
    public async Task RepositoryWriteLoad_RequiresExistingUnitOfWorkTransaction()
    {
        var repository = new SessionRepository(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.GetSettlementAsync(Guid.NewGuid(), true));
    }

    public void Dispose() => db.Dispose();
}
