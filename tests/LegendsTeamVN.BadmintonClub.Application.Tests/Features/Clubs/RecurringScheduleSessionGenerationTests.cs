using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Requests;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Create;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Schedules.Update;
using LegendsTeamVN.BadmintonClub.Application.Tests.Common;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Clubs;

public class RecurringScheduleSessionGenerationTests
{
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public void RecurringScheduleEngine_CalculateOccurrences_ShouldGenerateFutureSlotsWithin14Days()
    {
        // Giả lập thời điểm hiện tại: Thứ 2 (2026-10-19) lúc 08:00 UTC (15:00 giờ VN)
        var fakeNow = new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero);

        var schedules = new List<GroupSchedule>
        {
            // Thứ 2 lúc 18:00 - 21:00 (hôm nay lúc 18h VN)
            new(Guid.NewGuid(), (int)DayOfWeek.Monday, new TimeOnly(18, 0), new TimeOnly(21, 0)),
            // Thứ 4 lúc 19:00 - 22:00
            new(Guid.NewGuid(), (int)DayOfWeek.Wednesday, new TimeOnly(19, 0), new TimeOnly(22, 0)),
            // Thứ 6 lúc 18:00 - 20:00
            new(Guid.NewGuid(), (int)DayOfWeek.Friday, new TimeOnly(18, 0), new TimeOnly(20, 0))
        };

        var occurrences = RecurringScheduleEngine.CalculateOccurrences(schedules, fakeNow, daysAhead: 14);

        // Trong 14 ngày tới sẽ có các buổi Thứ 2, Thứ 4, Thứ 6
        occurrences.Should().NotBeEmpty();
        occurrences.All(o => o.StartUtc > fakeNow.UtcDateTime).Should().BeTrue();
        occurrences.All(o => o.EndUtc > o.StartUtc).Should().BeTrue();
    }

    [Fact]
    public void RecurringScheduleEngine_ShouldSkipOccurrencesWhereDeadlinePassed()
    {
        // Hiện tại: Thứ 2 lúc 17:00 giờ VN (10:00 UTC)
        // Lịch Thứ 2 bắt đầu lúc 18:00 (hạn chốt giữ chỗ là 16:00, đã qua)
        var fakeNow = new DateTimeOffset(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);

        var schedules = new List<GroupSchedule>
        {
            new(Guid.NewGuid(), (int)DayOfWeek.Monday, new TimeOnly(18, 0), new TimeOnly(21, 0))
        };

        var occurrences = RecurringScheduleEngine.CalculateOccurrences(schedules, fakeNow, daysAhead: 1);

        // Buổi hôm nay (hạn chốt 16:00 đã qua) sẽ không được tạo
        occurrences.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateClubSchedules_ShouldAutomaticallyGenerateBadmintonSessions()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var clubRepo = new ClubRepository(dbContext);
        var scheduleRepo = new ScheduleRepository(dbContext);
        var sessionRepo = new SessionRepository(dbContext);

        // Thứ 2 ngày 2026-10-19 03:00 UTC (10:00 VN)
        var fakeTime = new DateTimeOffset(2026, 10, 19, 3, 0, 0, TimeSpan.Zero);
        var fakeClock = new FixedClock(fakeTime);

        var club = new ClubGroup(
            groupName: "CLB Cầu Lông Tân Bình",
            fixedVenueName: "Sân Cầu Lông Viettel",
            fixedAddress: "158 Hoàng Hoa Thám, Tân Bình"
        );
        dbContext.ClubGroups.Add(club);
        await dbContext.SaveChangesAsync();

        var handler = new UpdateClubSchedulesCommandHandler(clubRepo, scheduleRepo, sessionRepo, fakeClock);

        var request = new UpdateClubSchedulesRequest(
            Schedules: new List<ClubScheduleItemRequest>
            {
                new(DayOfWeek: (int)DayOfWeek.Tuesday, StartTime: "18:00", EndTime: "21:00"),
                new(DayOfWeek: (int)DayOfWeek.Thursday, StartTime: "18:00", EndTime: "21:00")
            }
        );

        var command = new UpdateClubSchedulesCommand(club.Id, request);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();

        var savedSchedules = await dbContext.GroupSchedules.Where(s => s.GroupId == club.Id).ToListAsync();
        savedSchedules.Should().HaveCount(2);

        var generatedSessions = await dbContext.BadmintonSessions.Where(s => s.GroupId == club.Id).ToListAsync();
        generatedSessions.Should().NotBeEmpty();

        foreach (var session in generatedSessions)
        {
            session.CourtLocation.Should().Be("Sân Cầu Lông Viettel, 158 Hoàng Hoa Thám, Tân Bình");
            session.CourtCount.Should().Be(1);
            session.TargetPlayerPerCourt.Should().Be(6);
            session.MaxPlayers.Should().Be(6);
            session.Status.Should().Be(SessionStatus.OPEN);
            session.DeadlineBooking.Should().Be(session.StartTime.AddHours(-2));
        }

        // Test tính Idempotent: chạy lại không bị duplicate session
        var countBefore = generatedSessions.Count;
        dbContext.ChangeTracker.Clear();
        var repeatResult = await handler.Handle(command, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        repeatResult.IsSuccess.Should().BeTrue();
        var countAfter = await dbContext.BadmintonSessions.CountAsync(s => s.GroupId == club.Id);
        countAfter.Should().Be(countBefore);
    }

    [Fact]
    public async Task CreateClub_WithSchedules_ShouldGenerateInitialSessions()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var clubRepo = new ClubRepository(dbContext);
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(Guid.NewGuid());

        var fakeTime = new DateTimeOffset(2026, 10, 19, 3, 0, 0, TimeSpan.Zero);
        var fakeClock = new FixedClock(fakeTime);

        var handler = new CreateClubCommandHandler(clubRepo, currentUserService, fakeClock);

        var command = new CreateClubCommand(
            Name: "CLB Cầu Lông Thủ Đức",
            FixedVenueName: "Sân Cầu Lông Linh Trung",
            FixedAddress: "Đường số 6, Linh Trung, Thủ Đức",
            Schedules: new List<CreateClubScheduleItemRequest>
            {
                new(DayOfWeek: (short)DayOfWeek.Saturday, StartTime: new TimeOnly(17, 0), EndTime: new TimeOnly(20, 0))
            }
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        var createdClub = await dbContext.ClubGroups.Include(c => c.Sessions).FirstAsync(c => c.Id == result.Value);
        createdClub.Sessions.Should().NotBeEmpty();
        createdClub.Sessions.All(s => s.Status == SessionStatus.OPEN).Should().BeTrue();
        createdClub.Sessions.All(s => s.CourtLocation == "Sân Cầu Lông Linh Trung, Đường số 6, Linh Trung, Thủ Đức").Should().BeTrue();
    }
}
