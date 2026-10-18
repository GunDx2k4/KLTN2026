using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.ProcessDue;

public sealed class ProcessDueRotationCommandHandler(ISessionRepository sessions, TimeProvider clock,
    IOptionsMonitor<SessionRotationOptions> options) : ICommandHandler<ProcessDueRotationCommand>
{
    public async Task<Result> Handle(ProcessDueRotationCommand request, CancellationToken cancellationToken)
    {
        var session = await sessions.GetRotationAsync(request.SessionId, true, cancellationToken);
        if (session is null) return Result.Success();
        var known = session.Matches.Select(m => m.Id).ToHashSet();
        session.ProcessScheduledRotation(clock.GetUtcNow().UtcDateTime, options.CurrentValue.Enabled, request.Recover);
        sessions.AddRotationMatches(session.Matches.Where(m => !known.Contains(m.Id)));
        return Result.Success();
    }
}
