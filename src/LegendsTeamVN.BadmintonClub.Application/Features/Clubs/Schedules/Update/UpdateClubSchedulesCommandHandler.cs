using System.Globalization;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Schedules.Update;

public sealed class UpdateClubSchedulesCommandHandler(
    IClubRepository clubRepository,
    IScheduleRepository scheduleRepository) : ICommandHandler<UpdateClubSchedulesCommand>
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
            }
        }

        return Result.Success();
    }
}
