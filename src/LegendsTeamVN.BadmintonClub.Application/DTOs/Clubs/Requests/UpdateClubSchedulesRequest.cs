namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Requests;

public sealed record ClubScheduleItemRequest(
    int DayOfWeek,
    string StartTime,
    string EndTime
);

public sealed record UpdateClubSchedulesRequest(
    List<ClubScheduleItemRequest> Schedules
);
