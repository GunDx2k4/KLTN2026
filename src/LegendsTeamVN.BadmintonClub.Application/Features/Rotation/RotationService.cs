using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation;

public sealed class RotationService(ISessionRepository sessions, ICurrentUserService currentUser,
    TimeProvider clock, IOptionsMonitor<SessionRotationOptions> options)
{
    private async Task<Result<BadmintonSession>> Load(Guid id, bool write, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actor)
            return Result.Failure<BadmintonSession>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        var resource = await sessions.FindSingleAsync(s => s.Id == id && !s.IsDeleted, ct);
        if (resource is null) return Result.Failure<BadmintonSession>(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));
        if (!await sessions.CanManageRotationAsync(resource.GroupId, actor, ct))
            return Result.Failure<BadmintonSession>(Error.Forbidden("Rotation.Forbidden", "Bạn không có quyền điều phối tại nhóm này."));
        var session = await sessions.GetRotationAsync(id, write, ct);
        return session is null ? Result.Failure<BadmintonSession>(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi.")) : Result.Success(session);
    }

    public async Task<Result<RotationResponse>> GetAsync(Guid id, CancellationToken ct)
    {
        var loaded = await Load(id, false, ct);
        return loaded.IsFailure ? Result.Failure<RotationResponse>(loaded.Error) :
            Result.Success(Map(loaded.Value!, clock.GetUtcNow().UtcDateTime));
    }

    public async Task<Result<RotationResponse>> ChangeAsync(Guid id, string action, Guid? matchId, CancellationToken ct)
    {
        var loaded = await Load(id, true, ct);
        if (loaded.IsFailure) return Result.Failure<RotationResponse>(loaded.Error);
        // Emergency stop blocks new allocation/start, but must still allow finishing existing games.
        if (action != "finish" && !options.CurrentValue.Enabled)
            return Result.Failure<RotationResponse>(Error.Conflict("Rotation.Disabled", "Điều phối đang tạm tắt."));
        var session = loaded.Value!;
        var knownMatches = session.Matches.Select(m => m.Id).ToHashSet();
        var now = clock.GetUtcNow().UtcDateTime;
        var result = action switch
        {
            "initialize" => session.InitializeRotation(currentUser.UserId!.Value, now),
            "proposals" => session.GenerateRotationProposals(now),
            "start" => session.StartRotationMatch(matchId!.Value, now),
            "finish" => session.FinishRotationMatch(matchId!.Value, now, options.CurrentValue.Enabled),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
        if (result.IsFailure) return Result.Failure<RotationResponse>(result.Error);
        sessions.AddRotationMatches(session.Matches.Where(m => !knownMatches.Contains(m.Id)));
        return Result.Success(Map(session, now));
    }

    private RotationResponse Map(BadmintonSession session, DateTime now)
    {
        var response = RotationResponseMapper.Map(session, now);
        return options.CurrentValue.Enabled ? response : response with { CanInitialize = false, CanGenerateProposals = false };
    }
}
