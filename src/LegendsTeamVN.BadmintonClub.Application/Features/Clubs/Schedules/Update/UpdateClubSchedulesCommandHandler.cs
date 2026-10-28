using System.Globalization;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Schedules.Update;

public sealed class UpdateClubSchedulesCommandHandler(
    IClubRepository clubRepository,
    IScheduleRepository scheduleRepository,
    ISessionRepository sessionRepository,
    TimeProvider clock) : ICommandHandler<UpdateClubSchedulesCommand>
{
    public async Task<Result> Handle(UpdateClubSchedulesCommand command, CancellationToken cancellationToken)
    {
        var club = await clubRepository.FindByIdAsync(command.ClubId, cancellationToken);
        if (club is null)
        {
            return Result.Failure(Error.NotFound("Club.NotFound", "Không tìm thấy câu lạc bộ."));
        }

        var existingSchedules = await scheduleRepository.FindAll(s => s.GroupId == command.ClubId).ToListAsync(cancellationToken);
        scheduleRepository.RemoveMultiple(existingSchedules);

        var newSchedules = new List<GroupSchedule>();
        foreach (var item in command.Request.Schedules)
        {
            if (TimeOnly.TryParse(item.StartTime, CultureInfo.InvariantCulture, out var startTime) &&
                TimeOnly.TryParse(item.EndTime, CultureInfo.InvariantCulture, out var endTime))
            {
                var newSchedule = new GroupSchedule(
                    groupId: command.ClubId,
                    dayOfWeek: item.DayOfWeek,
                    startTime: startTime,
                    endTime: endTime,
                    isActive: true
                );

                scheduleRepository.Add(newSchedule);
                newSchedules.Add(newSchedule);
            }
        }

        // Tự động sinh lịch tập cho 14 ngày tới dựa trên khung giờ cố định
        var nowUtc = clock.GetUtcNow();
        var maxHorizonUtc = nowUtc.AddDays(14).UtcDateTime;
        var existingStartTimes = await sessionRepository.FindAll(s => s.GroupId == command.ClubId && !s.IsDeleted && s.StartTime >= nowUtc.UtcDateTime && s.StartTime <= maxHorizonUtc)
            .Select(s => s.StartTime)
            .ToListAsync(cancellationToken);

        var occurrences = RecurringScheduleEngine.CalculateOccurrences(newSchedules, nowUtc, daysAhead: 14);

        foreach (var (schedule, startUtc, endUtc) in occurrences)
        {
            // Tránh tạo trùng lặp nếu đã có buổi chơi cùng mốc thời gian bắt đầu (dung sai 30 phút)
            if (!existingStartTimes.Any(t => Math.Abs((t - startUtc).TotalMinutes) < 30))
            {
                var session = club.CreateRecurringSession(schedule, startUtc, endUtc);
                sessionRepository.Add(session);
                existingStartTimes.Add(startUtc);
            }
        }

        return Result.Success();
    }
}
