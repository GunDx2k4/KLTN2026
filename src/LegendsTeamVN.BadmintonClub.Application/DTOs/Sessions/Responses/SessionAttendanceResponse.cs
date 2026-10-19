using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;

public sealed record SessionAttendanceResponse(
    Guid SessionId, Guid GroupId, DateTime StartTime, DateTime EndTime,
    int MaxPlayers, int PresentCount, int NotArrivedCount, int NoShowCount, int WaitlistCount,
    int ReadyCount, bool CanEdit, bool CanStartRotation,
    DateTime? AttendanceCompletedAt, Guid? AttendanceCompletedBy,
    IReadOnlyList<AttendancePlayerResponse> Players);

public sealed record AttendancePlayerResponse(
    Guid BookingId, Guid UserId, string FullName, string? AvatarUrl, string SkillLevel,
    BookingStatus Status, DateTime? CheckinAt, DateTime? AttendanceMarkedAt,
    Guid? AttendanceMarkedBy, bool IsReady);
