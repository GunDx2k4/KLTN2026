using LegendsTeamVN.BadmintonClub.Domain.Entities;

namespace LegendsTeamVN.BadmintonClub.Domain.Services;

public static class RecurringScheduleEngine
{
    public static readonly TimeSpan VietnamTimeOffset = TimeSpan.FromHours(7);

    /// <summary>
    /// Tính toán các mốc thời gian bắt đầu và kết thúc (UTC) cho các buổi chơi trong vòng daysAhead ngày
    /// dựa trên danh sách khung giờ cố định.
    /// </summary>
    public static IReadOnlyList<(GroupSchedule Schedule, DateTime StartUtc, DateTime EndUtc)> CalculateOccurrences(
        IEnumerable<GroupSchedule> schedules,
        DateTimeOffset nowUtc,
        int daysAhead = 14)
    {
        var occurrences = new List<(GroupSchedule Schedule, DateTime StartUtc, DateTime EndUtc)>();
        var nowVn = nowUtc.ToOffset(VietnamTimeOffset);
        var todayVn = DateOnly.FromDateTime(nowVn.DateTime);

        var activeSchedules = schedules.Where(s => s.IsActive).ToList();
        if (activeSchedules.Count == 0)
        {
            return occurrences;
        }

        for (int i = 0; i <= daysAhead; i++)
        {
            var dateVn = todayVn.AddDays(i);
            var dayOfWeek = (int)dateVn.DayOfWeek;

            foreach (var schedule in activeSchedules.Where(s => s.DayOfWeek == dayOfWeek))
            {
                var startDateTimeVn = dateVn.ToDateTime(schedule.StartTime);
                var endDateVn = schedule.EndTime > schedule.StartTime ? dateVn : dateVn.AddDays(1);
                var endDateTimeVn = endDateVn.ToDateTime(schedule.EndTime);

                var startOffset = new DateTimeOffset(startDateTimeVn, VietnamTimeOffset);
                var endOffset = new DateTimeOffset(endDateTimeVn, VietnamTimeOffset);

                var startUtc = startOffset.UtcDateTime;
                var endUtc = endOffset.UtcDateTime;

                // Chỉ tạo các buổi chơi mà hạn chốt đăng ký (startUtc - 2h) vẫn còn trong tương lai so với thời điểm hiện tại
                if (startUtc.AddHours(-2) > nowUtc.UtcDateTime)
                {
                    occurrences.Add((schedule, startUtc, endUtc));
                }
            }
        }

        return occurrences;
    }
}
