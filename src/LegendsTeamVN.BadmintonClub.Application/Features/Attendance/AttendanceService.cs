using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Attendance;

public sealed class AttendanceService(
    ISessionRepository sessions, IBookingRepository bookings, IUserRepository users,
    ICurrentUserService currentUser, TimeProvider clock)
{
    private static Result<SessionAttendanceResponse> Fail(Error error) => Result.Failure<SessionAttendanceResponse>(error);

    private async Task<Result<BadmintonSession>> LoadAsync(Guid sessionId, bool forUpdate, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid userId)
            return Result.Failure<BadmintonSession>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        // Resolve the resource's real group before checking permission. No header/JWT tenant override.
        var session = await sessions.FindSingleAsync(s => s.Id == sessionId && !s.IsDeleted, ct);
        if (session is null)
            return Result.Failure<BadmintonSession>(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));
        if (!await sessions.CanManageAttendanceAsync(session.GroupId, userId, ct))
            return Result.Failure<BadmintonSession>(Error.Forbidden("Attendance.Forbidden", "Bạn không có quyền điểm danh tại nhóm của buổi chơi."));
        // Read with the parent row locked before checking time, capacity and modifying attendance.
        session = await sessions.GetAttendanceAsync(sessionId, forUpdate, ct);
        if (session is null)
            return Result.Failure<BadmintonSession>(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));
        if (forUpdate && !session.CanEditAttendance(clock.GetUtcNow().UtcDateTime))
            return Result.Failure<BadmintonSession>(Error.Conflict("Attendance.Closed", "Chỉ được điểm danh từ 1 giờ trước khi bắt đầu đến trước giờ kết thúc, khi buổi chưa chốt chi phí hoặc hoàn thành."));
        return Result.Success(session);
    }

    public async Task<Result<SessionAttendanceResponse>> GetAsync(Guid sessionId, CancellationToken ct)
    {
        var loaded = await LoadAsync(sessionId, false, ct);
        return loaded.IsFailure ? Fail(loaded.Error) : Result.Success(AttendanceResponseMapper.Map(loaded.Value!, clock.GetUtcNow().UtcDateTime));
    }

    public async Task<Result<SessionAttendanceResponse>> UpdateAsync(Guid sessionId, Guid userId, BookingStatus status, CancellationToken ct)
    {
        if (status is not (BookingStatus.CHECKED_IN or BookingStatus.NO_SHOW) || userId == Guid.Empty)
            return Fail(Error.Validation("Attendance.InvalidRequest", "Chỉ chấp nhận CHECKED_IN hoặc NO_SHOW và UserId hợp lệ."));
        var loaded = await LoadAsync(sessionId, true, ct);
        if (loaded.IsFailure) return Fail(loaded.Error);
        var session = loaded.Value!;
        var now = clock.GetUtcNow().UtcDateTime;
        var changed = session.ChangeAttendance(userId, status, currentUser.UserId!.Value, now);
        if (changed.IsFailure) return Fail(changed.Error);
        return Result.Success(AttendanceResponseMapper.Map(session, now));
    }

    public async Task<Result<SessionAttendanceResponse>> AddWalkInAsync(Guid sessionId, Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty) return Fail(Error.Validation("Attendance.InvalidUser", "UserId không được để trống."));
        var loaded = await LoadAsync(sessionId, true, ct);
        if (loaded.IsFailure) return Fail(loaded.Error);
        var session = loaded.Value!;
        var user = await users.FindSingleAsync(u => u.Id == userId && u.IsActive && !u.IsDeleted, ct);
        if (user is null) return Fail(Error.NotFound("User.NotFound", "Không tìm thấy tài khoản người chơi đang hoạt động."));
        var existing = session.Bookings.FirstOrDefault(b => b.UserId == userId);
        var now = clock.GetUtcNow().UtcDateTime;
        var admitted = session.AdmitWalkIn(userId, currentUser.UserId!.Value, now);
        if (admitted.IsFailure) return Fail(admitted.Error);
        if (existing is null) bookings.Add(admitted.Value!);
        return Result.Success(AttendanceResponseMapper.Map(session, now));
    }

    public async Task<Result<SessionAttendanceResponse>> CompleteAsync(Guid sessionId, CancellationToken ct)
    {
        var loaded = await LoadAsync(sessionId, true, ct);
        if (loaded.IsFailure) return Fail(loaded.Error);
        var session = loaded.Value!;
        var now = clock.GetUtcNow().UtcDateTime;
        var completed = session.CompleteAttendance(currentUser.UserId!.Value, now);
        if (completed.IsFailure) return Fail(completed.Error);
        return Result.Success(AttendanceResponseMapper.Map(session, now));
    }
}
