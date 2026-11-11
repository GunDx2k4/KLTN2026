using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
using LegendsTeamVN.BadmintonClub.Application.Features.Reputation.ProcessSessionAssessment;
using LegendsTeamVN.BadmintonClub.Infrastructure.Services;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Reputation;

public sealed class ReputationWorkerTests
{
    [Fact]
    public async Task FailedSessionDoesNotStarveBatchAndEachCommandGetsFreshScope()
    {
        var failedId = Guid.NewGuid();
        var goodId = Guid.NewGuid();
        var repo = Substitute.For<IReputationReadRepository>();
        repo.FindDueAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), 32, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { failedId, goodId });
        var scopes = new List<Guid>();
        var services = new ServiceCollection().AddScoped(_ => repo).AddScoped<ISender>(_ =>
        {
            scopes.Add(Guid.NewGuid());
            var sender = Substitute.For<ISender>();
            sender.Send(Arg.Any<ProcessSessionAssessmentCommand>(), Arg.Any<CancellationToken>()).Returns(c =>
                c.Arg<ProcessSessionAssessmentCommand>().SessionId == failedId
                    ? Task.FromException<Result<AssessmentResponse>>(new InvalidOperationException("Injected failure"))
                    : Task.FromResult(Result.Success(new AssessmentResponse(goodId, "Finalized", false))));
            return sender;
        });
        await using var provider = services.BuildServiceProvider();
        var worker = new ReputationAssessmentWorker(provider.GetRequiredService<IServiceScopeFactory>(), ReputationEngineTests.Options(),
            new ReputationEngineTests.Clock(), NullLogger<ReputationAssessmentWorker>.Instance);
        await worker.RunOnceAsync(default);
        Assert.Equal(2, scopes.Count);
        await worker.RunOnceAsync(default);
        await repo.Received().FindDueAsync(Arg.Any<DateTime>(), Arg.Any<DateTime>(), 32,
            Arg.Is<IReadOnlyList<Guid>>(ids => ids.Contains(failedId)), Arg.Any<CancellationToken>());
    }
}
