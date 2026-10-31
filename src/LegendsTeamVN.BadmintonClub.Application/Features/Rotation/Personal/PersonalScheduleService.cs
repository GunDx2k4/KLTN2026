using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Personal;

public sealed class PersonalScheduleService(ISessionRepository sessions, ICurrentUserService currentUser,
    TimeProvider clock, IOptionsMonitor<SessionRotationOptions> options)
{
    private async Task<Result<BadmintonSession>> Load(Guid id, bool write, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actor)
            return Result.Failure<BadmintonSession>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        PersonalRotationSnapshot loaded;
        try { loaded = await sessions.GetPersonalRotationAsync(id, actor, write, ct); }
        catch (InvalidLineupSnapshotException)
        {
            return Result.Failure<BadmintonSession>(Error.Conflict("Rotation.InvalidSnapshot", "Dữ liệu gợi ý gốc không hợp lệ."));
        }
        catch (InvalidRotationPauseMetadataException)
        {
            return Result.Failure<BadmintonSession>(Error.Conflict("Rotation.InvalidPauseMetadata", "Dữ liệu tạm nghỉ không hợp lệ."));
        }
        if (loaded.Session is null)
            return Result.Failure<BadmintonSession>(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));
        if (!loaded.CanAccess)
            return Result.Failure<BadmintonSession>(Error.Forbidden("PersonalSchedule.Forbidden", "Chỉ người đã điểm danh tại buổi được truy cập lịch cá nhân."));
        if (!options.CurrentValue.PersonalScheduleEnabled)
            return Result.Failure<BadmintonSession>(Error.Conflict("PersonalSchedule.Disabled", "Lịch cá nhân đang tạm tắt."));
        return Result.Success(loaded.Session);
    }

    public async Task<Result<MyMatchesResponse>> GetAsync(Guid id, CancellationToken ct)
    {
        var loaded = await Load(id, false, ct);
        return loaded.IsFailure ? Result.Failure<MyMatchesResponse>(loaded.Error) :
            Result.Success(Map(loaded.Value!, clock.GetUtcNow().UtcDateTime));
    }

    public async Task<Result<MyMatchesResponse>> SetPauseAsync(Guid id, bool paused, long? expectedVersion, CancellationToken ct)
    {
        var loaded = await Load(id, true, ct);
        if (loaded.IsFailure) return Result.Failure<MyMatchesResponse>(loaded.Error);
        if (expectedVersion is null || expectedVersion < 0)
            return Result.Failure<MyMatchesResponse>(Error.Validation("Rotation.VersionRequired", "Cần expectedRotationVersion không âm."));
        var flags = options.CurrentValue;
        if (paused && (!flags.Enabled || !flags.SelfPauseEnabled))
            return Result.Failure<MyMatchesResponse>(Error.Conflict("Rotation.SelfPauseDisabled", "Xin nghỉ đang tạm tắt."));
        var session = loaded.Value!;
        var known = session.Matches.Select(m => m.Id).ToHashSet();
        var now = clock.GetUtcNow().UtcDateTime;
        var result = session.SetSelfRotationPause(currentUser.UserId!.Value, paused, expectedVersion.Value, now, flags.Enabled);
        if (result.IsFailure) return Result.Failure<MyMatchesResponse>(result.Error);
        sessions.AddRotationMatches(session.Matches.Where(m => !known.Contains(m.Id)));
        return Result.Success(Map(session, now));
    }

    private MyMatchesResponse Map(BadmintonSession session, DateTime now)
    {
        var flags = options.CurrentValue;
        return MyMatchesResponseMapper.Map(session, currentUser.UserId!.Value, now,
            PersonalMatchSchedulePolicy.Evaluate(session, currentUser.UserId.Value, now,
                flags.Enabled, flags.PersonalScheduleEnabled, flags.SelfPauseEnabled));
    }
}
