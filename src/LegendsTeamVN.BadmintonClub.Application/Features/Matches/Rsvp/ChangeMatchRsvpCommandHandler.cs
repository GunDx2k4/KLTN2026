using LegendsTeamVN.BadmintonClub.Application.Abstractions.Identity;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Matches.Responses;
using LegendsTeamVN.BadmintonClub.Application.Models.Matches;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Models;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Logging;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Matches.Rsvp;

public sealed class ChangeMatchRsvpCommandHandler(IMatchRepository repository, IUnitOfWork unitOfWork, IMatchRsvpLock rsvpLock,
    IMatchAttendanceNotifier notifier, ICurrentUserContext currentUser,
    ILogger<ChangeMatchRsvpCommandHandler> logger) : ICommandHandler<ChangeMatchRsvpCommand, MatchResponse>
{
    public async Task<Result<MatchResponse>> Handle(ChangeMatchRsvpCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId)
            return Result.Failure<MatchResponse>(Error.Unauthorized("User.Unauthorized", "Sign in to RSVP."));

        Result<RsvpChange> result;
        await using (var lease = await rsvpLock.TryAcquireAsync(request.MatchId, cancellationToken))
        {
            if (lease is null)
                return Result.Failure<MatchResponse>(Error.Conflict("Match.RsvpBusy", "RSVP is temporarily unavailable. Please try again."));

            result = await ChangeAsync(request, userId, cancellationToken);
        }

        if (result.IsFailure) return Result.Failure<MatchResponse>(result.Error);

        if (result.Value.Changed)
        {
            // The transaction has committed. Broadcast failure cannot undo the saved RSVP.
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var match = result.Value.Match;
                var attendance = new MatchAttendanceSnapshot(match.Id, match.ConfirmedCount, match.AvailableSlots,
                    match.WaitlistCount, match.Status, match.MyStatus, match.MyWaitlistPosition, match.AttendanceVersion);
                await notifier.NotifyAsync(attendance, userId, timeout.Token);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "RSVP saved, but realtime notification failed for match {MatchId}", request.MatchId);
            }
        }

        return Result.Success(result.Value.Match);
    }

    private async Task<Result<RsvpChange>> ChangeAsync(ChangeMatchRsvpCommand request, Guid userId,
        CancellationToken cancellationToken)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken: cancellationToken);
        try
        {
            var result = await ApplyChangeAsync(request, userId, cancellationToken);
            if (result.IsFailure)
            {
                await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
                return result;
            }
            if (result.Value.Changed) await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
            return result;
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<Result<RsvpChange>> ApplyChangeAsync(ChangeMatchRsvpCommand request, Guid userId,
        CancellationToken cancellationToken)
    {
        var match = await repository.FindForRsvpAsync(request.MatchId, cancellationToken);
        if (match is null || match.BookingDetail.IsDeleted)
            return Result.Failure<RsvpChange>(Error.NotFound("Match.NotFound", "The match was not found."));

        var now = DateTimeOffset.UtcNow;
        var players = match.MatchPlayers.ToList();
        var decision = MatchRsvpRules.Decide(match, match.BookingDetail.TimeStart, players,
            userId, request.Withdraw, request.JoinWaitlist, now);
        if (decision.IsFailure) return Result.Failure<RsvpChange>(decision.Error);

        var changed = decision.Value.Status.HasValue;
        if (decision.Value.Status is MatchPlayerStatus status)
        {
            var player = players.SingleOrDefault(x => x.UserId == userId);
            if (player is null)
            {
                player = new MatchPlayer(match.Id, userId, userId == match.HostId, status);
                repository.AddPlayer(player);
                // Keep the aggregate complete for decision/response calculation, including test repositories.
                if (!match.MatchPlayers.Contains(player)) match.MatchPlayers.Add(player);
            }
            if (decision.Value.ResetJoinedAt) player.Register(status, now);
            else player.UpdateStatus(status);
            match.RecordAttendanceChange(match.MatchPlayers.Count(x => !x.IsDeleted && x.Status == MatchPlayerStatus.Approved));
        }

        return Result.Success(new RsvpChange(MatchResponseMapper.Map(match, userId), changed));
    }
}
