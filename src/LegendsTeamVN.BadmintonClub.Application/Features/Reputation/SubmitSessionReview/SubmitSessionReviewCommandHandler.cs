using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;
namespace LegendsTeamVN.BadmintonClub.Application.Features.Reputation.SubmitSessionReview;
public sealed class SubmitSessionReviewCommandHandler(IReputationRepository repository, ICurrentUserService currentUser,
    TimeProvider clock) : ICommandHandler<SubmitSessionReviewCommand, SessionReviewResponse>
{
    public async Task<Result<SessionReviewResponse>> Handle(SubmitSessionReviewCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actor) return Result.Failure<SessionReviewResponse>(ReputationErrors.Unauthorized);
        if (!await repository.IsActiveUserAsync(actor, ct)) return Result.Failure<SessionReviewResponse>(ReputationErrors.Forbidden);
        var source = await repository.LockSessionAsync(request.SessionId, ct);
        if (source is null) return Result.Failure<SessionReviewResponse>(ReputationErrors.NotFound);
        var booking = source.Session.Bookings.SingleOrDefault(x => x.UserId == actor && !x.IsDeleted);
        if (booking is null) return Result.Failure<SessionReviewResponse>(ReputationErrors.Forbidden);
        var old = await repository.FindReviewAsync(request.SessionId, actor, ct);
        if (old is not null)
            return old.MatchesSubmission(request.Rating, request.Comment)
                ? Result.Success(ToResponse(old)) : Result.Failure<SessionReviewResponse>(ReputationErrors.Conflict);
        GroupReview review;
        try { review = GroupReview.CreateForSession(source.Session, booking, request.Rating, request.Comment, clock.GetUtcNow().UtcDateTime); }
        catch (InvalidOperationException) { return Result.Failure<SessionReviewResponse>(ReputationErrors.Conflict); }
        repository.Add(review);
        var quality = await repository.GetQualitySourcesAsync(source.Group.Id, ct);
        source.Group.RebuildQuality(quality.Ratings, quality.Sessions, clock.GetUtcNow().UtcDateTime);
        return Result.Success(ToResponse(review));
    }
    private static SessionReviewResponse ToResponse(GroupReview review) =>
        new(review.Id, review.SessionId!.Value, review.Rating, review.Comment, review.CreatedAt);
}
