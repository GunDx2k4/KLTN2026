using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;
namespace LegendsTeamVN.BadmintonClub.Application.Features.Reputation.ProcessSessionAssessment;
public sealed class ProcessSessionAssessmentCommandHandler(IReputationRepository repository,
    IOptionsMonitor<ReputationOptions> options, TimeProvider clock)
    : ICommandHandler<ProcessSessionAssessmentCommand, AssessmentResponse>
{
    public async Task<Result<AssessmentResponse>> Handle(ProcessSessionAssessmentCommand request, CancellationToken ct)
    {
        var settings = options.CurrentValue;
        var source = await repository.LockSessionAsync(request.SessionId, ct);
        if (source is null || source.Session.StartTime < settings.EffectiveFromUtc)
            return Result.Failure<AssessmentResponse>(ReputationErrors.NotFound);
        var session = source.Session;
        if (session.ReputationProcessingStatus == ReputationProcessingStatus.Finalized)
            return Result.Success(new AssessmentResponse(session.Id, "Finalized", true));
        var now = clock.GetUtcNow().UtcDateTime;
        if (!session.AssessReputation(source.Quality, source.Fees, source.Ledger, source.Banks, now))
            return Result.Success(new AssessmentResponse(session.Id, session.ReputationProcessingStatus.ToString(), false));
        var users = await repository.LockUsersAsync(session.ReputationSubjects().Select(x => x.UserId).Distinct().ToArray(), ct);
        var history = await repository.GetHistoriesAsync(users.Select(u => u.Id).ToArray(), ct);
        foreach (var user in users) user.RebuildReputation(history[user.Id], now);
        var quality = await repository.GetQualitySourcesAsync(session.GroupId, ct);
        source.Group.RebuildQuality(quality.Ratings, quality.Sessions, now);
        return Result.Success(new AssessmentResponse(session.Id, "Finalized", false));
    }
}
