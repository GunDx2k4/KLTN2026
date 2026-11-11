using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Notifications;
using LegendsTeamVN.BadmintonClub.Application.Features.Reputation.Events;
using LegendsTeamVN.BadmintonClub.Application.Features.Reputation.ProcessSessionAssessment;
using LegendsTeamVN.BadmintonClub.Application.Features.Reputation.SubmitSessionReview;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.Core.Identity.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Reputation;

public sealed class ReputationEngineTests
{
    internal static readonly DateTime Now = new(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc);
    internal sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    internal static IOptionsMonitor<ReputationOptions> Options()
    {
        var options = Substitute.For<IOptionsMonitor<ReputationOptions>>();
        options.CurrentValue.Returns(new ReputationOptions { EffectiveFromUtc = new DateTimeOffset(Now.AddDays(-30)) });
        return options;
    }
    internal static (ReputationAssessmentSource Source, AppUser User) Source(decimal price = 0, int daysAgo = 1, AppUser? user = null, bool late = false, ClubGroup? group = null)
    {
        user ??= new AppUser(Guid.NewGuid(), "UC20 Player");
        group ??= new ClubGroup(groupName: "UC20 Club");
        var end = Now.AddDays(-daysAgo);
        var session = new BadmintonSession(group.Id, "UC20", end.AddHours(-2), end, "Court", courtCount: 1);
        var booking = new SessionBooking(session.Id, user.Id);
        booking.MarkPresent(user.Id, session.StartTime.AddMinutes(late ? 20 : 0));
        session.Bookings.Add(booking);
        var rule = new GroupFeeRule(group.Id, FeeMode.FIXED, dailyMemberFee: price);
        var member = new GroupMember(group.Id, user.Id, Guid.NewGuid(), "DAILY");
        var result = session.PublishSettlement(rule, [user], [member], new(0, 0, 0, 0), [],
            Guid.NewGuid(), new string('a', 64), user.Id, end);
        Assert.True(result.IsSuccess, result.Error.Message);
        session.UpdateStatus(SessionStatus.COMPLETED);
        return (new(session, group, session.Fees.ToArray(), [], [], new(0, 0, 0, 0, 1, 0)), user);
    }
    internal static IReputationRepository Repository(ReputationAssessmentSource source, AppUser user)
    {
        var repo = Substitute.For<IReputationRepository>();
        repo.LockSessionAsync(source.Session.Id, Arg.Any<CancellationToken>()).Returns(source);
        repo.LockUsersAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>()).Returns(new[] { user });
        repo.GetHistoriesAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>()).Returns(_ =>
            new Dictionary<Guid, IReadOnlyList<ReputationScoreEntry>> { [user.Id] = source.Session.Bookings
                .Where(b => b.ReputationComputedAt.HasValue).Select(b => new ReputationScoreEntry(b.SessionId, source.Session.EndTime, b.ReputationRawDelta!.Value)).ToArray() });
        repo.GetQualitySourcesAsync(source.Group.Id, Arg.Any<CancellationToken>())
            .Returns(_ => ((IReadOnlyList<int>)Array.Empty<int>(), (IReadOnlyList<QualitySessionEvidence>)new[] { source.Quality }));
        repo.IsActiveUserAsync(user.Id, Arg.Any<CancellationToken>()).Returns(true);
        repo.GetActiveUserIdsAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>()).Returns(new[] { user.Id });
        return repo;
    }
    [Fact]
    public async Task UnpaidDefersWithoutUserProjectionOrEvents()
    {
        var (source, user) = Source(100);
        source.Session.ClearDomainEvents();
        var repo = Repository(source, user);
        var result = await new ProcessSessionAssessmentCommandHandler(repo, Options(), new Clock()).Handle(new(source.Session.Id), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(ReputationProcessingStatus.Pending, source.Session.ReputationProcessingStatus);
        Assert.True(source.Session.ReputationNextAttemptAt > Now);
        Assert.Null(source.Session.Bookings.Single().ReputationComputedAt);
        Assert.Empty(source.Session.DomainEvents);
        await repo.DidNotReceiveWithAnyArgs().LockUsersAsync(default!, default);
    }
    [Fact]
    public async Task ExemptFinalizesWithoutReviewAndReplayDoesNotRebuild()
    {
        var (source, user) = Source();
        var repo = Repository(source, user);
        var handler = new ProcessSessionAssessmentCommandHandler(repo, Options(), new Clock());
        Assert.True((await handler.Handle(new(source.Session.Id), default)).IsSuccess);
        Assert.Equal(100, user.ReputationBaselineScore);
        Assert.Equal(1, source.Session.Bookings.Single().ReputationRawDelta);
        Assert.NotNull(source.Group.QualityComputedAt);
        Assert.True((await handler.Handle(new(source.Session.Id), default)).Value.Replayed);
        await repo.Received(1).LockUsersAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>());
        Assert.Single(source.Session.DomainEvents.OfType<SessionFinancialReconciledDomainEvent>());
    }
    [Fact]
    public async Task MissingReceiptDefersAndCashReceiptAllowsFinalize()
    {
        var (source, user) = Source(100);
        source.Fees.Single().ConfirmCashPayment(source.Session, user.Id, source.Session.EndTime.AddHours(1));
        Assert.False(source.Session.AssessReputation(source.Quality, source.Fees, [], [], Now));
        Assert.Equal(ReputationProcessingStatus.DataInsufficient, source.Session.ReputationProcessingStatus);
        var receipt = GroupLedger.CreateSessionFeeReceipt(source.Session, source.Fees.Single());
        source = source with { Ledger = new[] { receipt } };
        var result = await new ProcessSessionAssessmentCommandHandler(Repository(source, user), Options(), new Clock()).Handle(new(source.Session.Id), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, source.Session.Bookings.Single().ReputationRawDelta);
    }
    [Fact]
    public async Task InvitationsUseStableEventIdsAndMinimalPayloads()
    {
        var (source, user) = Source();
        source.Session.AssessReputation(source.Quality, source.Fees, [], [], Now);
        var repo = Repository(source, user);
        repo.GetFinalizedAsync(source.Session.Id, Arg.Any<CancellationToken>()).Returns(source.Session);
        var messages = new List<OutboxMessage>();
        repo.When(r => r.Add(Arg.Any<OutboxMessage>())).Do(c => messages.Add(c.Arg<OutboxMessage>()));
        var handler = new SessionFinancialReconciledDomainEventHandler(repo);
        await handler.Handle(new(source.Session.Id, source.Group.Id, Now), default);
        await handler.Handle(new(source.Session.Id, source.Group.Id, Now), default);
        Assert.Equal(4, messages.Count);
        Assert.Equal(new[] { "ReputationUpdatedPush", "ReviewInvitationPush" }, messages.Take(2).Select(m => m.Kind));
        Assert.Equal(messages[0].Payload, messages[2].Payload);
        var invitation = JsonSerializer.Deserialize<ReputationPush>(messages[1].Payload)!;
        Assert.Equal(Now.AddDays(7), invitation.ExpiresAt);
        Assert.Equal(user.Id, invitation.RecipientId);
        Assert.DoesNotContain("Score", messages[1].Payload);
    }
    [Fact]
    public async Task GuestCanReviewAndReplayDoesNotChangeQualityOrReputation()
    {
        var (source, user) = Source();
        source.Session.AssessReputation(source.Quality, source.Fees, [], [], Now);
        var repo = Repository(source, user);
        var actor = Substitute.For<ICurrentUserService>(); actor.UserId.Returns(user.Id);
        GroupReview? review = null;
        repo.When(r => r.Add(Arg.Any<GroupReview>())).Do(c => review = c.Arg<GroupReview>());
        var handler = new SubmitSessionReviewCommandHandler(repo, actor, new Clock());
        var first = await handler.Handle(new(source.Session.Id, 5, " tốt "), default);
        Assert.True(first.IsSuccess);
        Assert.Equal("tốt", first.Value.Comment);
        repo.FindReviewAsync(source.Session.Id, user.Id, Arg.Any<CancellationToken>()).Returns(review);
        var version = source.Group.QualityVersion;
        Assert.Equal(first.Value, (await handler.Handle(new(source.Session.Id, 5, "tốt"), default)).Value);
        Assert.Equal("Reputation.Conflict", (await handler.Handle(new(source.Session.Id, 4, "tốt"), default)).Error.Code);
        Assert.Equal(version, source.Group.QualityVersion);
        Assert.Null(user.ReputationComputedAt);
        repo.Received(1).Add(Arg.Any<GroupReview>());
    }
    [Fact]
    public async Task StrangerCannotSubmitReview()
    {
        var (source, user) = Source();
        var repo = Repository(source, user);
        var stranger = Guid.NewGuid();
        repo.IsActiveUserAsync(stranger, Arg.Any<CancellationToken>()).Returns(true);
        var actor = Substitute.For<ICurrentUserService>(); actor.UserId.Returns(stranger);
        var result = await new SubmitSessionReviewCommandHandler(repo, actor, new Clock()).Handle(new(source.Session.Id, 5, null), default);
        Assert.Equal("Reputation.Forbidden", result.Error.Code);
        repo.DidNotReceive().Add(Arg.Any<GroupReview>());
    }
    [Theory]
    [InlineData(0, 0)]
    [InlineData(6, 0)]
    [InlineData(3, 2001)]
    public async Task ReviewValidatorRejectsInvalidInput(int rating, int length)
    {
        var result = await new SubmitSessionReviewCommandValidator().ValidateAsync(new SubmitSessionReviewCommand(Guid.NewGuid(), rating, new string('a', length)));
        Assert.False(result.IsValid);
    }
}
