using LegendsTeamVN.BadmintonClub.Application.DTOs.Settlement.Requests;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;
using LegendsTeamVN.BadmintonClub.Application.Features.Settlement.GetForm;
using LegendsTeamVN.BadmintonClub.Application.Features.Settlement.Preview;
using LegendsTeamVN.BadmintonClub.Application.Features.Settlement.Approve;
using LegendsTeamVN.BadmintonClub.Application.Features.Settlement.Get;
using LegendsTeamVN.BadmintonClub.Application.Features.Settlement.MarkMemberPaid;
using LegendsTeamVN.BadmintonClub.Application.Tests.Common;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Sessions;

public sealed class SettlementRotationSafetyTests
{
    private static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
    private sealed class Fixture
    {
        public Guid Actor { get; } = Guid.NewGuid();
        public BadmintonSession Session { get; } = new(Guid.NewGuid(), "Quyết toán", Now.AddHours(-1), Now.AddHours(2), "Sân", courtCount: 2);
        public ISessionRepository Sessions { get; } = Substitute.For<ISessionRepository>();
        public ICurrentUserService User { get; } = Substitute.For<ICurrentUserService>();
        public ISettlementPreviewTokenService Tokens { get; } = Substitute.For<ISettlementPreviewTokenService>();
        public IOptionsMonitor<SettlementOptions> Options { get; } = Substitute.For<IOptionsMonitor<SettlementOptions>>();
        public MutableClock Clock { get; } = new();
        public PreviewSettlementRequest Input { get; } = new(2, 25000m, 300000m, 0m, []);
        public SettlementPreviewIdentity Identity { get; }
        public Fixture()
        {
            User.UserId.Returns(Actor);
            Sessions.CanManageFinanceAsync(Session.GroupId, Actor, Arg.Any<CancellationToken>()).Returns(true);
            Sessions.CanViewFinanceAsync(Session.GroupId, Actor, Arg.Any<CancellationToken>()).Returns(true);
            for (var i = 0; i < 12; i++)
            {
                var user = new AppUser(Guid.NewGuid(), "Người chơi " + i);
                var booking = new SessionBooking(Session.Id, user.Id);
                booking.MarkPresent(Actor, Now.AddMinutes(-10).AddSeconds(i));
                typeof(SessionBooking).GetProperty(nameof(SessionBooking.User))!.SetValue(booking, user);
                Session.Bookings.Add(booking);
            }
            Assert.True(Session.CompleteAttendance(Actor, Now).IsSuccess);
            Assert.True(Session.InitializeRotation(Actor, Now).IsSuccess);
            Session.ClearDomainEvents();
            Clock.Now = Session.EndTime;
            Identity = new(1, SettlementOptions.CurrentPolicyVersion, Guid.NewGuid(), Actor, Session.GroupId, Session.Id,
                Clock.Now, Clock.Now.AddMinutes(15), new string('a', 64), "source", "result");
            Sessions.GetSettlementAsync(Session.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(Session);
            Sessions.GetSettlementSourceAsync(Session.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns((Session, new GroupFeeRule(Session.GroupId), Session.Bookings.Select(b => b.User).ToArray(), Array.Empty<GroupMember>()));
            Tokens.HashSource(Arg.Any<BadmintonSession>(), Arg.Any<GroupFeeRule>(), Arg.Any<IReadOnlyCollection<AppUser>>(),
                Arg.Any<IReadOnlyCollection<GroupMember>>()).Returns("source");
            Tokens.HashResult(Arg.Any<SettlementAllocation>()).Returns("result");
            Tokens.Create(Actor, Session.GroupId, Session.Id, Arg.Any<SettlementCosts>(), Arg.Any<IReadOnlyCollection<SettlementAdjustment>>(),
                Arg.Any<bool>(), "source", Arg.Any<SettlementAllocation>(), Clock.Now).Returns(new SettlementPreviewToken("preview", Identity));
            Tokens.Validate(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<SettlementCosts>(),
                Arg.Any<IReadOnlyCollection<SettlementAdjustment>>(), Arg.Any<bool>()).Returns(Result.Failure<SettlementPreviewIdentity>(
                    Error.Conflict("Settlement.InvalidPreviewToken", "Token không hợp lệ.")));
            Tokens.Validate("preview", Actor, Session.GroupId, Session.Id, new SettlementCosts(2, 25000m, 300000m, 0m),
                Arg.Any<IReadOnlyCollection<SettlementAdjustment>>(), Arg.Any<bool>()).Returns(Result.Success(Identity));
            Options.CurrentValue.Returns(new SettlementOptions { WritesEnabled = true });
        }
        public PreviewSettlementQueryHandler Preview() => new(Sessions, User, Tokens, Clock);
        public ApproveSettlementCommandHandler Approve() => new(Sessions, User, Tokens, Clock, Options);
        public SessionBooking AddPending()
        {
            var booking = new SessionBooking(Session.Id, Guid.NewGuid());
            Session.Bookings.Add(booking);
            return booking;
        }
    }

    [Fact]
    public async Task PreviewIsReadOnly_ApprovalClosesRotationAndRegistersFees()
    {
        var f = new Fixture(); var session = f.Session; var version = session.RotationVersion;
        var preview = await f.Preview().Handle(new(session.Id, f.Input), default);
        Assert.True(preview.IsSuccess); Assert.Equal(350000m, preview.Value.Allocation.TotalCost);
        Assert.Empty(session.Fees); Assert.Null(session.PublishedPreviewId); Assert.Equal(version, session.RotationVersion);
        Assert.All(session.Matches, m => Assert.Equal(MatchStatus.WAITING, m.Status)); Assert.Empty(session.DomainEvents);
        f.Sessions.DidNotReceive().AddSettlementFees(Arg.Any<IEnumerable<SessionFee>>());
        using var db = TestDbContextFactory.Create();
        f.Sessions.When(r => r.AddSettlementFees(Arg.Any<IEnumerable<SessionFee>>()))
            .Do(call => new SessionRepository(db).AddSettlementFees(call.Arg<IEnumerable<SessionFee>>()));
        var result = await f.Approve().Handle(new(session.Id, f.Input, preview.Value.PreviewToken), default);
        Assert.True(result.IsSuccess); Assert.Equal(SessionStatus.CALCULATED, session.Status); Assert.Null(session.NextProposalCheckAt);
        Assert.All(session.Matches, m => Assert.Equal(MatchStatus.SKIPPED, m.Status)); Assert.True(session.RotationVersion > version);
        Assert.Single(session.DomainEvents.OfType<RotationChangedDomainEvent>());
        Assert.Single(session.DomainEvents.OfType<SettlementPublishedDomainEvent>());
        Assert.Equal(12, session.Fees.Count); Assert.All(session.Fees, fee => Assert.Equal(EntityState.Added, db.Entry(fee).State));
        await db.SaveChangesAsync(); Assert.Equal(12, await db.SessionFees.CountAsync());
        await f.Sessions.Received(1).GetSettlementSourceAsync(session.Id, false, Arg.Any<CancellationToken>());
        await f.Sessions.Received(1).GetSettlementSourceAsync(session.Id, true, Arg.Any<CancellationToken>());
        f.Sessions.DidNotReceive().Update(Arg.Any<BadmintonSession>());
    }

    [Fact]
    public async Task PlayingMatchBlocksPreviewWithoutChangingSelfPauseOrExpenses()
    {
        var f = new Fixture(); var session = f.Session;
        var player = session.Bookings.First(b => !session.Matches.Any(m => m.Players.Any(p => p.UserId == b.UserId)));
        Assert.True(session.SetSelfRotationPause(player.UserId, true, session.RotationVersion, Now).IsSuccess);
        var target = session.Matches.First(m => m.Status == MatchStatus.WAITING);
        Assert.True(session.StartRotationMatch(target.Id, Now.AddSeconds(1)).IsSuccess);
        var version = session.RotationVersion; session.ClearDomainEvents();
        var result = await f.Preview().Handle(new(session.Id, f.Input), default);
        Assert.Equal("Session.ActiveMatches", result.Error.Code); Assert.Equal(version, session.RotationVersion);
        Assert.Equal(SessionStatus.PLAYING, session.Status); Assert.Empty(session.Fees); Assert.Empty(session.DomainEvents);
        Assert.True(player.IsRotationPaused); Assert.Equal(target.Id, player.SelfPauseTargetMatchId); Assert.Equal(0m, session.CourtRentalCost);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemainingConfirmedAreOnlyMarkedNoShowDuringConfirmedApproval(bool confirm)
    {
        var f = new Fixture(); var pending = f.AddPending(); var request = f.Input with { ConfirmRemainingNoShows = confirm };
        var preview = await f.Preview().Handle(new(f.Session.Id, request), default);
        Assert.Equal(BookingStatus.CONFIRMED, pending.Status); Assert.Null(pending.AttendanceMarkedAt);
        if (!confirm) { Assert.Equal("Attendance.NoShowConfirmationRequired", preview.Error.Code); return; }
        Assert.True(preview.IsSuccess);
        Assert.True((await f.Approve().Handle(new(f.Session.Id, request, "preview"), default)).IsSuccess);
        Assert.Equal(BookingStatus.NO_SHOW, pending.Status); Assert.Equal(f.Actor, pending.AttendanceMarkedBy);
        Assert.Equal(f.Clock.Now, pending.AttendanceMarkedAt); Assert.Equal(12, f.Session.Fees.Count);
    }

    [Theory]
    [InlineData("source", "Settlement.PreviewStale")]
    [InlineData("result", "Settlement.PreviewStale")]
    [InlineData("expired", "Settlement.PreviewExpired")]
    [InlineData("input", "Settlement.InvalidPreviewToken")]
    [InlineData("actor", "Settlement.InvalidPreviewToken")]
    [InlineData("writes", "Settlement.WritesDisabled")]
    public async Task InvalidApprovalNeverMutatesSessionFeesOrNoShows(string change, string expected)
    {
        var f = new Fixture(); var pending = f.AddPending(); var request = f.Input with { ConfirmRemainingNoShows = true };
        if (change == "source") f.Tokens.HashSource(Arg.Any<BadmintonSession>(), Arg.Any<GroupFeeRule>(),
            Arg.Any<IReadOnlyCollection<AppUser>>(), Arg.Any<IReadOnlyCollection<GroupMember>>()).Returns("changed");
        if (change == "result") f.Tokens.HashResult(Arg.Any<SettlementAllocation>()).Returns("changed");
        if (change == "expired") f.Clock.Now = f.Identity.ExpiresAt;
        if (change == "input") request = request with { CourtRentalCost = 1m };
        if (change == "writes") f.Options.CurrentValue.Returns(new SettlementOptions());
        if (change == "actor")
        {
            f.User.UserId.Returns(Guid.NewGuid());
            f.Sessions.CanManageFinanceAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        }
        var version = f.Session.RotationVersion;
        var result = await f.Approve().Handle(new(f.Session.Id, request, "preview"), default);
        Assert.Equal(expected, result.Error.Code); Assert.Equal(BookingStatus.CONFIRMED, pending.Status);
        Assert.Null(pending.AttendanceMarkedAt); Assert.Null(f.Session.PublishedAt); Assert.Empty(f.Session.Fees);
        Assert.Empty(f.Session.DomainEvents); Assert.Equal(version, f.Session.RotationVersion);
        Assert.All(f.Session.Matches, m => Assert.Equal(MatchStatus.WAITING, m.Status));
        f.Sessions.DidNotReceive().AddSettlementFees(Arg.Any<IEnumerable<SessionFee>>());
    }

    [Fact]
    public async Task RetryAfterPaymentAndExpiryReturnsOriginalPublication()
    {
        var f = new Fixture(); Assert.True((await f.Approve().Handle(new(f.Session.Id, f.Input, "preview"), default)).IsSuccess);
        var fee = f.Session.Fees.First(); fee.MarkAsPaid(PaymentMethod.CASH, f.Actor);
        var paidAt = fee.PaidAt; var eventCount = f.Session.DomainEvents.Count;
        f.Clock.Now = f.Identity.ExpiresAt.AddDays(1);
        f.Tokens.HashSource(Arg.Any<BadmintonSession>(), Arg.Any<GroupFeeRule>(), Arg.Any<IReadOnlyCollection<AppUser>>(),
            Arg.Any<IReadOnlyCollection<GroupMember>>()).Returns("changed");
        var result = await f.Approve().Handle(new(f.Session.Id, f.Input, "preview"), default);
        Assert.True(result.IsSuccess); Assert.Equal(12, f.Session.Fees.Count); Assert.Equal(PaymentStatus.PAID, fee.PaymentStatus);
        Assert.Equal(paidAt, fee.PaidAt); Assert.Equal(eventCount, f.Session.DomainEvents.Count);
        f.Sessions.Received(1).AddSettlementFees(Arg.Any<IEnumerable<SessionFee>>());
    }

    [Theory]
    [InlineData("preview")]
    [InlineData("approve")]
    [InlineData("get")]
    [InlineData("form")]
    public async Task HandlersRequirePermissionInActualSessionGroup(string action)
    {
        var f = new Fixture();
        f.Sessions.CanManageFinanceAsync(f.Session.GroupId, f.Actor, Arg.Any<CancellationToken>()).Returns(false);
        f.Sessions.CanViewFinanceAsync(f.Session.GroupId, f.Actor, Arg.Any<CancellationToken>()).Returns(false);
        var error = action switch
        {
            "preview" => (await f.Preview().Handle(new(f.Session.Id, f.Input), default)).Error,
            "approve" => (await f.Approve().Handle(new(f.Session.Id, f.Input, "preview"), default)).Error,
            "form" => (await new GetSettlementFormQueryHandler(f.Sessions, f.User).Handle(new(f.Session.Id), default)).Error,
            _ => (await new GetSessionSettlementQueryHandler(f.Sessions, f.User).Handle(new(f.Session.Id), default)).Error
        };
        Assert.Equal("Settlement.Forbidden", error.Code); Assert.Empty(f.Session.Fees); Assert.Empty(f.Session.DomainEvents);
    }

    [Fact]
    public async Task LegacyReadDoesNotInventSnapshotsOrRecalculateFees()
    {
        var f = new Fixture(); f.Session.Fees.Add(new SessionFee(f.Session.Id, f.Session.Bookings.First().UserId, 12345m, PriceType.GUEST, "OLD", ""));
        var result = await new GetSessionSettlementQueryHandler(f.Sessions, f.User).Handle(new(f.Session.Id), default);
        Assert.True(result.IsSuccess); Assert.Equal("LEGACY", result.Value.State); Assert.Null(result.Value.TotalCost);
        Assert.Null(result.Value.ShuttlecockPrice); var fee = Assert.Single(result.Value.MemberFees);
        Assert.Equal(12345m, fee.FinalFee); Assert.Null(fee.Breakdown);
        await f.Sessions.Received(1).GetSettlementAsync(f.Session.Id, false, Arg.Any<CancellationToken>());
        await f.Sessions.DidNotReceive().GetSettlementAsync(f.Session.Id, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RepositoryWriteLoadRequiresExistingTransaction()
    {
        using var db = TestDbContextFactory.Create();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new SessionRepository(db).GetSettlementAsync(Guid.NewGuid(), true));
    }

    [Fact]
    public async Task MarkPaidOnlyModifiesFeeAndPreservesActiveRotationAndSelfPause()
    {
        var f = new Fixture(); var session = f.Session;
        var player = session.Bookings.First(b => !session.Matches.Any(m => m.Players.Any(p => p.UserId == b.UserId)));
        Assert.True(session.SetSelfRotationPause(player.UserId, true, session.RotationVersion, Now).IsSuccess);
        var target = session.Matches.First(m => m.Status == MatchStatus.WAITING);
        Assert.True(session.StartRotationMatch(target.Id, Now.AddSeconds(1)).IsSuccess);
        var fee = new SessionFee(session.Id, player.UserId, 50000m, PriceType.GUEST, "OLD", "");
        session.Fees.Add(fee); session.ClearDomainEvents(); var version = session.RotationVersion;
        using var db = TestDbContextFactory.Create(); db.Attach(session);
        var result = await new MarkMemberPaidCommandHandler(f.Sessions, f.User).Handle(new(session.Id, player.UserId), default);
        db.ChangeTracker.DetectChanges();
        Assert.True(result.IsSuccess); Assert.Equal(PaymentStatus.PAID, fee.PaymentStatus);
        Assert.Equal(EntityState.Modified, db.Entry(fee).State); Assert.Equal(EntityState.Unchanged, db.Entry(session).State);
        Assert.Equal(version, session.RotationVersion); Assert.Equal(SessionStatus.PLAYING, session.Status);
        Assert.True(player.IsRotationPaused); Assert.Equal(target.Id, player.SelfPauseTargetMatchId);
        Assert.Equal(MatchStatus.PLAYING, target.Status); Assert.Empty(session.DomainEvents);
        Assert.Equal("PAID", Assert.Single(result.Value.MemberFees).PaymentStatus);
        f.Sessions.DidNotReceive().Update(Arg.Any<BadmintonSession>());
    }

    [Fact]
    public async Task PublishedReadUsesStoredAmountsWhenWritesAreDisabled()
    {
        var f = new Fixture(); Assert.True((await f.Approve().Handle(new(f.Session.Id, f.Input, "preview"), default)).IsSuccess);
        f.Options.CurrentValue.Returns(new SettlementOptions());
        var result = await new GetSessionSettlementQueryHandler(f.Sessions, f.User).Handle(new(f.Session.Id), default);
        Assert.True(result.IsSuccess); Assert.Equal("PUBLISHED", result.Value.State);
        Assert.Equal(350000m, result.Value.TotalCost); Assert.Equal(25000m, result.Value.ShuttlecockPrice);
        Assert.Equal(360000m, result.Value.TotalReceivable); Assert.Equal(10000m, result.Value.RoundingDelta);
        Assert.All(result.Value.MemberFees, fee => { Assert.Equal(30000m, fee.FinalFee); Assert.NotNull(fee.Breakdown); });
        Assert.Equal(f.Identity.PreviewId, f.Session.PublishedPreviewId);
    }
    private sealed class MutableClock : TimeProvider
    {
        public DateTime Now { get; set; }
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
}