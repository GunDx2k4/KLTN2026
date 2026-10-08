using System.Data;
using System.Linq.Expressions;
using LegendsTeamVN.BadmintonClub.Application.Abstractions.Identity;
using LegendsTeamVN.BadmintonClub.Domain.Models;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Matches.Responses;
using LegendsTeamVN.BadmintonClub.Application.Features.Matches.Rsvp;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Behaviors;
using LegendsTeamVN.Core.Domain.Repositories;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests;

public sealed class ChangeMatchRsvpCommandHandlerTests
{
    private readonly User user = new();
    private readonly Lock rsvpLock = new();
    private readonly Notifier notifier = new();
    private readonly UnitOfWork unitOfWork = new();
    private readonly Repository repository;

    public ChangeMatchRsvpCommandHandlerTests()
    {
        var now = DateTimeOffset.UtcNow;
        var detail = new BookingDetail(Guid.NewGuid(), Guid.NewGuid(), now.AddHours(2), now.AddHours(3), 100);
        var match = new Match(detail.Id, Guid.NewGuid(), 1, 100, now.AddHours(1));
        // Populate the navigation as EF would when loading the entity.
        typeof(Match).GetProperty(nameof(Match.BookingDetail))!.SetValue(match, detail);
        repository = new Repository(match, unitOfWork);
        repository.BeforeRead = () => Assert.True(unitOfWork.Active);
    }

    private ChangeMatchRsvpCommandHandler Handler() => new(repository, unitOfWork, rsvpLock, notifier, user,
        NullLogger<ChangeMatchRsvpCommandHandler>.Instance);
    private ChangeMatchRsvpCommand Command() => new(repository.Match.Id, false);

    [Fact]
    public async Task AnonymousCallerCannotReachLockOrPersistence()
    {
        user.IsAuthenticated = false;
        var result = await Handler().Handle(Command(), default);
        Assert.Equal("User.Unauthorized", result.Error.Code);
        Assert.Equal(0, rsvpLock.Calls);
        Assert.Equal(0, repository.Reads);
        Assert.False(unitOfWork.Active);
    }

    [Fact]
    public async Task BusyOrUnavailableRedisDoesNotOpenTransaction()
    {
        rsvpLock.Available = false;
        var result = await Handler().Handle(Command(), default);
        Assert.Equal("Match.RsvpBusy", result.Error.Code);
        Assert.Equal(0, repository.Reads);
        Assert.Equal(0, unitOfWork.Begins);
        Assert.Equal(0, notifier.Calls);
    }

    [Fact]
    public async Task WaitlistConsentFailureRollsBackAndReleasesLock()
    {
        repository.Match.MatchPlayers.Add(new MatchPlayer(repository.Match.Id, Guid.NewGuid(), status: MatchPlayerStatus.Approved));
        var result = await Handler().Handle(Command(), default);
        Assert.Equal("Match.WaitlistConfirmationRequired", result.Error.Code);
        Assert.True(unitOfWork.RolledBack);
        Assert.False(unitOfWork.Committed);
        Assert.Equal(0, unitOfWork.Saves);
        Assert.True(rsvpLock.Released);
        Assert.Equal(0, notifier.Calls);
        Assert.Single(repository.Match.MatchPlayers);
    }

    [Fact]
    public async Task PersistenceExceptionRollsBackAndReleasesLock()
    {
        repository.Throw = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Handler().Handle(Command(), default));
        Assert.True(unitOfWork.RolledBack);
        Assert.True(rsvpLock.Released);
        Assert.Equal(0, notifier.Calls);
    }

    [Fact]
    public async Task BroadcastRunsAfterCommitAndLockReleaseWithAuthenticatedUserId()
    {
        notifier.BeforeSend = () =>
        {
            Assert.True(unitOfWork.Committed);
            Assert.True(rsvpLock.Released);
        };
        var result = await Handler().Handle(Command(), default);
        Assert.True(result.IsSuccess);
        var player = Assert.Single(repository.Match.MatchPlayers);
        Assert.Equal(user.UserId, player.UserId);
        Assert.Equal(user.UserId, notifier.LastUserId);
        Assert.Equal(MatchPlayerStatus.Approved, result.Value.MyStatus);
        Assert.Equal(1, unitOfWork.Saves);
        Assert.Equal(1, notifier.Calls);
    }

    [Fact]
    public async Task RealtimeFailureDoesNotTurnCommittedRsvpIntoFailure()
    {
        notifier.Throw = true;
        var result = await Handler().Handle(Command(), default);
        Assert.True(result.IsSuccess);
        Assert.True(unitOfWork.Committed);
        Assert.False(unitOfWork.RolledBack);
    }

    [Fact]
    public async Task IdempotentRequestDoesNotSaveOrBroadcast()
    {
        var player = new MatchPlayer(repository.Match.Id, user.UserId!.Value, status: MatchPlayerStatus.Approved);
        repository.Match.MatchPlayers.Add(player);
        var joinedAt = player.JoinedAt;
        var result = await Handler().Handle(Command(), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(0, unitOfWork.Saves);
        Assert.Equal(0, notifier.Calls);
        Assert.Equal(joinedAt, player.JoinedAt);
        Assert.Equal(0L, repository.Match.AttendanceVersion);
    }

    [Fact]
    public async Task PipelineDoesNotWrapRsvpInAnOuterTransaction()
    {
        var pipeline = new UnitOfWorkBehavior<ChangeMatchRsvpCommand, Result<MatchResponse>>(unitOfWork);
        var result = await pipeline.Handle(Command(), _ => Handler().Handle(Command(), default), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, unitOfWork.Begins);
    }

    private sealed class User : ICurrentUserContext
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public bool IsAuthenticated { get; set; } = true;
    }

    private sealed class Repository(Match match, IUnitOfWork unitOfWork) : IMatchRepository
    {
        public Match Match => match;
        public IUnitOfWork UnitOfWork => unitOfWork;
        public int Reads { get; private set; }
        public bool Throw { get; set; }
        public Action? BeforeRead { get; set; }
        public Task<Match?> FindForRsvpAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Reads++;
            BeforeRead?.Invoke();
            if (Throw) throw new InvalidOperationException("Simulated database failure.");
            return Task.FromResult<Match?>(id == match.Id ? match : null);
        }
        public Task<Match?> FindDetailsAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Match?>(match);
        public void AddPlayer(MatchPlayer player) { }
        public Task<Match?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default, params Expression<Func<Match, object>>[] includes) => throw new NotSupportedException();
        public Task<Match?> FindSingleAsync(Expression<Func<Match, bool>>? predicate = null, CancellationToken cancellationToken = default, params Expression<Func<Match, object>>[] includes) => throw new NotSupportedException();
        public IQueryable<Match> FindAll(Expression<Func<Match, bool>>? predicate = null, params Expression<Func<Match, object>>[] includes) => throw new NotSupportedException();
        public void Add(Match entity) => throw new NotSupportedException();
        public void Update(Match entity) => throw new NotSupportedException();
        public void Remove(Match entity) => throw new NotSupportedException();
        public void RemoveMultiple(IEnumerable<Match> entities) => throw new NotSupportedException();
    }

    private sealed class UnitOfWork : IUnitOfWork, IDisposable
    {
        public bool Active { get; private set; }
        public bool Committed { get; private set; }
        public bool RolledBack { get; private set; }
        public int Begins { get; private set; }
        public int Saves { get; private set; }
        public Task<IDisposable> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default)
        {
            Assert.False(Active);
            Active = true;
            Begins++;
            return Task.FromResult<IDisposable>(this);
        }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Assert.True(Active);
            Saves++;
            return Task.FromResult(1);
        }
        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            Assert.True(Active);
            Committed = true;
            Active = false;
            return Task.CompletedTask;
        }
        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            RolledBack = true;
            Active = false;
            return Task.CompletedTask;
        }
        public void Dispose() { }
    }

    private sealed class Lock : IMatchRsvpLock, IAsyncDisposable
    {
        public bool Available { get; set; } = true;
        public int Calls { get; private set; }
        public bool Released { get; private set; }
        public Task<IAsyncDisposable?> TryAcquireAsync(Guid matchId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IAsyncDisposable?>(Available ? this : null);
        }
        public ValueTask DisposeAsync()
        {
            Released = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Notifier : IMatchAttendanceNotifier
    {
        public int Calls { get; private set; }
        public Guid LastUserId { get; private set; }
        public bool Throw { get; set; }
        public Action? BeforeSend { get; set; }
        public Task NotifyAsync(MatchAttendanceSnapshot match, Guid userId, CancellationToken cancellationToken)
        {
            Calls++;
            LastUserId = userId;
            BeforeSend?.Invoke();
            if (Throw) throw new InvalidOperationException("Simulated realtime failure.");
            return Task.CompletedTask;
        }
    }
}
