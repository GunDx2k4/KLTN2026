using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Notifications;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
using LegendsTeamVN.BadmintonClub.Application.Features.Reputation.Events;
using LegendsTeamVN.BadmintonClub.Application.Features.Reputation.ProcessSessionAssessment;
using LegendsTeamVN.BadmintonClub.Application.Features.Reputation.SubmitSessionReview;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.BadmintonClub.Persistence;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using LegendsTeamVN.Core.Application.Behaviors;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Reputation;

public sealed class ReputationTransactionTests
{
    private sealed class Publisher(bool fail) : IPublisher
    {
        public BadmintonDbContext Db { get; set; } = null!;
        public Task Publish(object notification, CancellationToken ct = default) => Dispatch(notification, ct);
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default) where TNotification : INotification => Dispatch(notification, ct);
        private async Task Dispatch(object notification, CancellationToken ct)
        {
            if (notification is not SessionFinancialReconciledDomainEvent reconciled) return;
            await new SessionFinancialReconciledDomainEventHandler(new ReputationRepository(Db)).Handle(reconciled, ct);
            if (fail) throw new InvalidOperationException("Injected Outbox failure");
        }
    }
    internal static async Task<Result<AssessmentResponse>> Process(ReputationTestDatabase fixture, Guid sessionId, bool fail = false)
    {
        var publisher = new Publisher(fail);
        await using var db = fixture.Open(publisher);
        publisher.Db = db;
        var handler = new ProcessSessionAssessmentCommandHandler(new ReputationRepository(db), ReputationEngineTests.Options(), new ReputationEngineTests.Clock());
        var command = new ProcessSessionAssessmentCommand(sessionId);
        return await new UnitOfWorkBehavior<ProcessSessionAssessmentCommand, Result<AssessmentResponse>>(db)
            .Handle(command, ct => handler.Handle(command, ct), default);
    }
    internal static async Task<(Guid SessionId, Guid UserId, Guid GroupId)> Seed(ReputationTestDatabase fixture, AppUser? user = null, int daysAgo = 1, bool late = false, decimal price = 0, Guid? groupId = null)
    {
        await using var db = fixture.Open();
        var group = groupId.HasValue ? await db.ClubGroups.SingleAsync(g => g.Id == groupId.Value) : null;
        var (source, player) = ReputationEngineTests.Source(user: user, daysAgo: daysAgo, late: late, price: price, group: group);
        if (!await db.AppUsers.AnyAsync(u => u.Id == player.Id)) db.Add(player);
        if (group is null) db.Add(source.Group);
        db.Add(source.Session);
        await db.SaveChangesAsync();
        return (source.Session.Id, player.Id, source.Group.Id);
    }
    [ReputationIntegrationFact]
    public async Task FinalizationAndInvitationsCommitOnceAcrossConcurrentRetries()
    {
        await using var fixture = await ReputationTestDatabase.Create();
        var seed = await Seed(fixture);
        var results = await Task.WhenAll(Process(fixture, seed.SessionId), Process(fixture, seed.SessionId));
        Assert.All(results, r => Assert.True(r.IsSuccess));
        Assert.Single(results, r => r.Value.Replayed);
        await using var db = fixture.Open();
        Assert.Equal(ReputationProcessingStatus.Finalized, (await db.BadmintonSessions.SingleAsync()).ReputationProcessingStatus);
        Assert.Equal(2, await db.OutboxMessages.CountAsync());
        Assert.Equal(2, await db.Notifications.CountAsync());
        Assert.Equal(100, (await db.AppUsers.SingleAsync()).ReputationScore);
        var read = new ReputationReadRepository(db);
        Assert.Single((await read.GetEntriesAsync(seed.UserId, 1, 20, default)).Items);
        Assert.Single((await read.GetInvitationsAsync(seed.UserId, ReputationEngineTests.Now, 1, 20, default)).Items);
        Assert.Equal(1, (await read.GetPlayerAsync(seed.UserId, default))!.SampleCount);
        Assert.NotNull(await read.GetQualityAsync(seed.GroupId, default));
        Assert.Null(await read.GetAuditAsync(Guid.NewGuid(), seed.SessionId, 1, 20, default));
    }
    [ReputationIntegrationFact]
    public async Task OutboxFailureRollsBackAllAndRetrySucceeds()
    {
        await using var fixture = await ReputationTestDatabase.Create();
        var seed = await Seed(fixture);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Process(fixture, seed.SessionId, true));
        await using (var db = fixture.Open())
        {
            Assert.Null((await db.BadmintonSessions.SingleAsync()).ReputationSnapshot);
            Assert.Null((await db.AppUsers.SingleAsync()).ReputationBaselineScore);
            Assert.Null((await db.SessionBookings.SingleAsync()).ReputationComputedAt);
            Assert.Equal(0, await db.OutboxMessages.CountAsync());
            Assert.Equal(0, await db.Notifications.CountAsync());
        }
        Assert.True((await Process(fixture, seed.SessionId)).IsSuccess);
    }
    [ReputationIntegrationFact]
    public async Task LateOlderSessionRebuildsAcrossGroupsAndSoftDeletedSources()
    {
        await using var fixture = await ReputationTestDatabase.Create();
        var user = new AppUser(Guid.NewGuid(), "Shared player");
        var older = await Seed(fixture, user, 2, late: true);
        var newer = await Seed(fixture, user, 1);
        await Process(fixture, newer.SessionId);
        await using (var db = fixture.Open())
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE session_booking SET \"IsDeleted\"=true WHERE id_session={newer.SessionId}");
        await Process(fixture, older.SessionId);
        await using var verify = fixture.Open();
        Assert.Equal(98, (await verify.AppUsers.SingleAsync()).ReputationScore);
        Assert.Equal(2, (await new ReputationReadRepository(verify).GetEntriesAsync(user.Id, 1, 20, default)).Total);
    }
    [ReputationIntegrationFact]
    public async Task TwoGroupsSharingUserDoNotLoseUpdates()
    {
        await using var fixture = await ReputationTestDatabase.Create();
        var user = new AppUser(Guid.NewGuid(), "Shared player");
        var older = await Seed(fixture, user, 2, late: true);
        var newer = await Seed(fixture, user, 1, late: true);
        var results = await Task.WhenAll(Process(fixture, older.SessionId), Process(fixture, newer.SessionId));
        Assert.All(results, r => Assert.True(r.IsSuccess));
        await using var verify = fixture.Open();
        Assert.Equal(94, (await verify.AppUsers.SingleAsync()).ReputationScore);
        Assert.Equal(4, await verify.OutboxMessages.CountAsync());
    }
    [ReputationIntegrationFact]
    public async Task ConcurrentReviewReplayDoesNotChangeScores()
    {
        await using var fixture = await ReputationTestDatabase.Create();
        var first = await Seed(fixture);
        await Process(fixture, first.SessionId);
        async Task<Result<SessionReviewResponse>> Submit(int rating)
        {
            await using var db = fixture.Open();
            var actor = Substitute.For<ICurrentUserService>(); actor.UserId.Returns(first.UserId);
            var handler = new SubmitSessionReviewCommandHandler(new ReputationRepository(db), actor, new ReputationEngineTests.Clock());
            var command = new SubmitSessionReviewCommand(first.SessionId, rating, "tốt");
            return await new UnitOfWorkBehavior<SubmitSessionReviewCommand, Result<SessionReviewResponse>>(db)
                .Handle(command, ct => handler.Handle(command, ct), default);
        }
        var results = await Task.WhenAll(Submit(5), Submit(5));
        Assert.All(results, r => Assert.True(r.IsSuccess));
        Assert.Equal(results[0].Value.ReviewId, results[1].Value.ReviewId);
        Assert.Equal("Reputation.Conflict", (await Submit(4)).Error.Code);
        await using var verify = fixture.Open();
        Assert.Equal(1, await verify.GroupReviews.CountAsync());
        Assert.Equal(5, (await verify.ClubGroups.SingleAsync()).GroupQualityScore);
        Assert.Equal(1, (await verify.ClubGroups.SingleAsync()).QualitySummary!.ReviewCount);
        Assert.Equal(2, await verify.OutboxMessages.CountAsync());
    }
    [ReputationIntegrationFact]
    public async Task SnapshotUsesDatabasePermissionsAndRemainsFrozenAfterRoleChange()
    {
        await using var fixture = await ReputationTestDatabase.Create();
        var seed = await Seed(fixture);
        Guid permissionId;
        await using (var db = fixture.Open())
        {
            var permission = new GroupPermission(GroupPermissions.Finance.LedgerView, "Xem sổ quỹ", ModuleGroup.FINANCE);
            permissionId = permission.Id;
            var role = new GroupRole(seed.GroupId, "Vai trò tùy chỉnh"); role.AddPermission(permission.Id);
            var member = new GroupMember(seed.GroupId, seed.UserId, role.Id, "DAILY");
            var guest = new AppUser(Guid.NewGuid(), "Guest");
            db.AddRange(permission, role, member, guest, new GroupMember(seed.GroupId, guest.Id, role.Id, "GUEST"));
            await db.SaveChangesAsync();
        }
        await Process(fixture, seed.SessionId);
        await using (var db = fixture.Open())
        {
            var snapshot = (await db.BadmintonSessions.SingleAsync()).ReputationSnapshot!;
            Assert.Equal(1, snapshot.Quality.MemberCount);
            Assert.Equal(1, snapshot.Quality.LedgerViewerCount);
            var role = await db.GroupRoles.Include(r => r.RolePermissions).SingleAsync();
            role.RemovePermission(permissionId);
            await db.SaveChangesAsync();
            Assert.False(await new ReputationReadRepository(db).CanAuditAsync(seed.GroupId, seed.UserId, default));
        }
        await Process(fixture, seed.SessionId);
        await using var verify = fixture.Open();
        Assert.Equal(1, (await verify.BadmintonSessions.SingleAsync()).ReputationSnapshot!.Quality.LedgerViewerCount);
    }
    [ReputationIntegrationFact]
    public async Task CashSettlementAndLedgerLocksCanRunAlongsideAssessment()
    {
        await using var fixture = await ReputationTestDatabase.Create();
        var seed = await Seed(fixture, price: 100);
        async Task Pay()
        {
            await using var db = fixture.Open();
            var feeId = await db.SessionFees.Select(f => f.Id).SingleAsync();
            await db.BeginTransactionAsync();
            var fee = (await new PaymentRepository(db).GetMineAsync(feeId, seed.UserId, true, default))!;
            fee.ConfirmCashPayment(fee.Session, seed.UserId, fee.Session.EndTime.AddHours(1));
            db.GroupLedgers.Add(GroupLedger.CreateSessionFeeReceipt(fee.Session, fee));
            await db.SaveChangesAsync(); await db.CommitTransactionAsync();
        }
        async Task SettlementRead()
        {
            await using var db = fixture.Open();
            await db.BeginTransactionAsync();
            Assert.NotNull((await new SessionRepository(db).GetSettlementSourceAsync(seed.SessionId, true, default)).Session);
            await db.CommitTransactionAsync();
        }
        async Task LedgerRead()
        {
            await using var db = fixture.Open();
            await db.BeginTransactionAsync();
            Assert.True(await new LedgerWriteRepository(db).LockGroupAsync(seed.GroupId, default));
            await db.CommitTransactionAsync();
        }
        await Task.WhenAll(Pay(), SettlementRead(), LedgerRead(), Process(fixture, seed.SessionId));
        Assert.True((await Process(fixture, seed.SessionId)).IsSuccess);
        await using var verify = fixture.Open();
        Assert.Equal(PaymentStatus.PAID, (await verify.SessionFees.SingleAsync()).PaymentStatus);
        Assert.Equal(1, await verify.GroupLedgers.CountAsync());
        Assert.Equal(2, (await verify.SessionBookings.SingleAsync()).ReputationRawDelta);
        Assert.Equal(2, await verify.OutboxMessages.CountAsync());
    }

    [ReputationIntegrationFact]
    public async Task TwoDifferentReviewsInSameGroupPreserveBothContributions()
    {
        await using var fixture = await ReputationTestDatabase.Create();
        var first = await Seed(fixture);
        var second = await Seed(fixture, groupId: first.GroupId);
        await Process(fixture, first.SessionId);
        await Process(fixture, second.SessionId);
        async Task Submit(Guid sessionId, Guid userId, int rating)
        {
            await using var db = fixture.Open();
            var actor = Substitute.For<ICurrentUserService>(); actor.UserId.Returns(userId);
            var handler = new SubmitSessionReviewCommandHandler(new ReputationRepository(db), actor, new ReputationEngineTests.Clock());
            var command = new SubmitSessionReviewCommand(sessionId, rating, null);
            var result = await new UnitOfWorkBehavior<SubmitSessionReviewCommand, Result<SessionReviewResponse>>(db)
                .Handle(command, ct => handler.Handle(command, ct), default);
            Assert.True(result.IsSuccess);
        }
        await Task.WhenAll(Submit(first.SessionId, first.UserId, 5), Submit(second.SessionId, second.UserId, 1));
        await using var verify = fixture.Open();
        var group = await verify.ClubGroups.SingleAsync(g => g.Id == first.GroupId);
        Assert.Equal(2, group.QualitySummary!.ReviewCount);
        Assert.Equal(3, group.GroupQualityScore);
        Assert.Equal(4, await verify.OutboxMessages.CountAsync());
    }

    [ReputationIntegrationFact]
    public async Task DeliveryEligibilityUsesOwnershipWindowAndSubmittedReview()
    {
        await using var fixture = await ReputationTestDatabase.Create();
        var seed = await Seed(fixture);
        await Process(fixture, seed.SessionId);
        await using var db = fixture.Open();
        var booking = await db.SessionBookings.SingleAsync();
        var session = await db.BadmintonSessions.SingleAsync();
        var read = new ReputationReadRepository(db);
        var expiry = session.FinancialReconciledAt!.Value.AddDays(7);
        Assert.True(await read.CanDeliverAsync("ReviewInvitationPush", session.Id, booking.Id, seed.UserId, expiry, ReputationEngineTests.Now, default));
        Assert.False(await read.CanDeliverAsync("ReviewInvitationPush", session.Id, booking.Id, Guid.NewGuid(), expiry, ReputationEngineTests.Now, default));
        Assert.False(await read.CanDeliverAsync("ReviewInvitationPush", session.Id, booking.Id, seed.UserId, expiry, expiry, default));
        db.Add(GroupReview.CreateForSession(session, booking, 4, null, ReputationEngineTests.Now));
        await db.SaveChangesAsync();
        Assert.False(await read.CanDeliverAsync("ReviewInvitationPush", session.Id, booking.Id, seed.UserId, expiry, ReputationEngineTests.Now, default));
        Assert.True(await read.CanDeliverAsync("ReputationUpdatedPush", session.Id, booking.Id, seed.UserId, null, ReputationEngineTests.Now, default));
    }

    [ReputationIntegrationFact]
    public async Task PersistedAssessmentOutboxIsDeliveredOnceWithoutRebuildingScoresOrClaimingOtherKinds()
    {
        await using var fixture = await ReputationTestDatabase.Create();
        var seed = await Seed(fixture);
        await Process(fixture, seed.SessionId);
        long version;
        await using (var db = fixture.Open())
        {
            version = (await db.AppUsers.SingleAsync()).ReputationVersion;
            db.Add(new OutboxMessage("LedgerUpdatedPush", seed.UserId, "{}", ReputationEngineTests.Now));
            await db.SaveChangesAsync();
        }
        var devices = Substitute.For<IDeviceRepository>();
        devices.GetActiveTokensAsync(seed.UserId, Arg.Any<CancellationToken>()).Returns(new[] { "sandbox-device" });
        var notifications = Substitute.For<INotificationService>();
        notifications.SendMulticastNotificationAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<CancellationToken>()).Returns(1);
        var realtime = Substitute.For<IReputationRealtimePublisher>();
        await using var provider = new ServiceCollection().AddScoped(_ => fixture.Open())
            .AddScoped<IOutboxRepository, OutboxRepository>().AddScoped<IReputationReadRepository, ReputationReadRepository>()
            .AddSingleton(devices).AddSingleton(notifications).AddSingleton(realtime).BuildServiceProvider();
        var worker = new ReputationNotificationWorker(provider.GetRequiredService<IServiceScopeFactory>(),
            new ReputationEngineTests.Clock(), NullLogger<ReputationNotificationWorker>.Instance);
        Assert.True(await worker.ProcessOnceAsync(default));
        Assert.True(await worker.ProcessOnceAsync(default));
        Assert.False(await worker.ProcessOnceAsync(default));
        await realtime.Received(2).PublishAsync(Arg.Any<string>(), Arg.Is<ReputationPush>(p => p.RecipientId == seed.UserId), Arg.Any<CancellationToken>());
        await using var verify = fixture.Open();
        Assert.Equal(2, await verify.OutboxMessages.CountAsync(m => m.DeliveredAt != null));
        Assert.Null((await verify.OutboxMessages.SingleAsync(m => m.Kind == "LedgerUpdatedPush")).DeliveredAt);
        Assert.Equal(version, (await verify.AppUsers.SingleAsync()).ReputationVersion);
    }

}
