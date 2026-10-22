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
        BadmintonSession? session;
        try { session = await sessions.GetRotationAsync(id, write, ct); }
        catch (InvalidLineupSnapshotException)
        {
            return Result.Failure<BadmintonSession>(Error.Conflict("Rotation.InvalidSnapshot",
                "Gợi ý gốc đã lưu không hợp lệ. Vui lòng kiểm tra dữ liệu buổi chơi trước khi tiếp tục."));
        }
        return session is null ? Result.Failure<BadmintonSession>(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi.")) : Result.Success(session);
    }

    public async Task<Result<RotationResponse>> GetAsync(Guid id, CancellationToken ct)
    {
        var loaded = await Load(id, false, ct);
        var isHost = loaded.IsSuccess && await sessions.CanEditManualRotationAsync(loaded.Value!.GroupId, currentUser.UserId!.Value, ct);
        var canEdit = isHost && options.CurrentValue.Enabled && options.CurrentValue.ManualEditingEnabled;
        return loaded.IsFailure ? Result.Failure<RotationResponse>(loaded.Error) :
            Result.Success(Map(loaded.Value!, clock.GetUtcNow().UtcDateTime, canEdit, isHost));
    }

    public async Task<Result<RotationResponse>> UpdateManualLineupAsync(Guid id, Guid matchId,
        IReadOnlyList<Guid>? teamA, IReadOnlyList<Guid>? teamB, long? expectedVersion, CancellationToken ct)
    {
        var loaded = await LoadManual(id, false, ct);
        if (loaded.IsFailure) return Result.Failure<RotationResponse>(loaded.Error);
        var session = loaded.Value!;
        if (expectedVersion is null)
            return Result.Failure<RotationResponse>(Error.Validation("Rotation.VersionRequired", "Cần expectedRotationVersion."));
        var match = session.Matches.FirstOrDefault(m => m.Id == matchId && !m.IsDeleted);
        var previous = match?.Players.ToArray() ?? [];
        var version = session.RotationVersion;
        var now = clock.GetUtcNow().UtcDateTime;
        var result = session.UpdateManualLineup(matchId, teamA, teamB, expectedVersion.Value, now);
        if (result.IsFailure) return Result.Failure<RotationResponse>(result.Error);
        if (session.RotationVersion != version) sessions.SynchronizeRotationPlayers(match!, previous);
        return Result.Success(Map(session, now, true));
    }

    private async Task<Result<BadmintonSession>> LoadManual(Guid id, bool allowDisabled, CancellationToken ct)
    {
        var loaded = await Load(id, true, ct);
        if (loaded.IsFailure) return loaded;
        // Recheck current Host/permission against the resource group after acquiring the session lock.
        if (!await sessions.CanEditManualRotationAsync(loaded.Value!.GroupId, currentUser.UserId!.Value, ct))
            return Result.Failure<BadmintonSession>(Error.Forbidden("Rotation.HostRequired", "Chỉ Host của nhóm được can thiệp thủ công."));
        if (!allowDisabled && (!options.CurrentValue.Enabled || !options.CurrentValue.ManualEditingEnabled))
            return Result.Failure<BadmintonSession>(Error.Conflict("Rotation.ManualDisabled", "Can thiệp thủ công đang tạm tắt."));
        return loaded;
    }

    public async Task<Result<RotationResponse>> RestoreManualLineupAsync(Guid id, Guid matchId, long? expectedVersion, CancellationToken ct)
    {
        var loaded = await LoadManual(id, false, ct);
        if (loaded.IsFailure) return Result.Failure<RotationResponse>(loaded.Error);
        if (expectedVersion is null)
            return Result.Failure<RotationResponse>(Error.Validation("Rotation.VersionRequired", "Cần expectedRotationVersion."));
        var session = loaded.Value!;
        var match = session.Matches.FirstOrDefault(m => m.Id == matchId && !m.IsDeleted);
        var previous = match?.Players.ToArray() ?? [];
        var version = session.RotationVersion;
        var now = clock.GetUtcNow().UtcDateTime;
        var result = session.RestoreManualLineup(matchId, expectedVersion.Value, now);
        if (result.IsFailure) return Result.Failure<RotationResponse>(result.Error);
        if (session.RotationVersion != version) sessions.SynchronizeRotationPlayers(match!, previous);
        return Result.Success(Map(session, now, true));
    }

    public async Task<Result<RotationResponse>> SetRotationPauseAsync(Guid id, Guid userId, bool? isPaused,
        long? expectedVersion, Guid? replacementUserId, CancellationToken ct)
    {
        var loaded = await LoadManual(id, isPaused == false, ct);
        if (loaded.IsFailure) return Result.Failure<RotationResponse>(loaded.Error);
        if (isPaused is null || expectedVersion is null)
            return Result.Failure<RotationResponse>(Error.Validation("Rotation.PauseFieldsRequired", "Cần isPaused và expectedRotationVersion."));
        var session = loaded.Value!;
        var previous = session.Matches.Where(m => !m.IsDeleted && m.Status == Domain.Enums.MatchStatus.WAITING)
            .ToDictionary(m => m.Id, m => m.Players.ToArray());
        var known = session.Matches.Select(m => m.Id).ToHashSet();
        var version = session.RotationVersion;
        var now = clock.GetUtcNow().UtcDateTime;
        var result = session.SetRotationPause(userId, isPaused.Value, expectedVersion.Value, replacementUserId, now, options.CurrentValue.Enabled);
        if (result.IsFailure) return Result.Failure<RotationResponse>(result.Error);
        if (session.RotationVersion != version)
        {
            foreach (var pair in previous)
                sessions.SynchronizeRotationPlayers(session.Matches.Single(m => m.Id == pair.Key), pair.Value);
            sessions.AddRotationMatches(session.Matches.Where(m => !known.Contains(m.Id)));
        }
        return Result.Success(Map(session, now, options.CurrentValue.Enabled && options.CurrentValue.ManualEditingEnabled, true));
    }

    public async Task<Result<RotationResponse>> ConfirmManualMatchAsync(Guid id, Guid matchId, long? expectedVersion, CancellationToken ct)
    {
        var loaded = await LoadManual(id, false, ct);
        if (loaded.IsFailure) return Result.Failure<RotationResponse>(loaded.Error);
        if (expectedVersion is null)
            return Result.Failure<RotationResponse>(Error.Validation("Rotation.VersionRequired", "Cần expectedRotationVersion."));
        var session = loaded.Value!;
        var now = clock.GetUtcNow().UtcDateTime;
        var result = session.ConfirmManualMatch(matchId, expectedVersion.Value, now);
        return result.IsFailure ? Result.Failure<RotationResponse>(result.Error) : Result.Success(Map(session, now, true));
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
        var isHost = await sessions.CanEditManualRotationAsync(session.GroupId, currentUser.UserId!.Value, ct);
        var canEdit = isHost && options.CurrentValue.Enabled && options.CurrentValue.ManualEditingEnabled;
        return Result.Success(Map(session, now, canEdit, isHost));
    }

    private RotationResponse Map(BadmintonSession session, DateTime now, bool canEdit = false, bool canResume = false)
    {
        var response = RotationResponseMapper.Map(session, now, canEdit, canResume || canEdit);
        return options.CurrentValue.Enabled ? response : response with { CanInitialize = false, CanGenerateProposals = false };
    }
}
