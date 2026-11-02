using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Application.Features.Recruitments.Explore;
using LegendsTeamVN.BadmintonClub.Application.Tests.Common;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Recruitments;

public class ExploreRecruitmentsTests
{
    [Fact]
    public async Task Handle_WhenHostHasActiveBooking_ShouldStillReturnRecruitment()
    {
        using var dbContext = TestDbContextFactory.Create();
        var recruitmentRepo = new RecruitmentRepository(dbContext);
        var clubMemberRepo = new ClubMemberRepository(dbContext);

        var hostUserId = Guid.NewGuid();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(hostUserId);

        var club = new ClubGroup(groupName: "CLB Test Host", code: "CLB01");
        dbContext.ClubGroups.Add(club);

        var hostRole = new GroupRole(club.Id, "Host", isDefault: false);
        dbContext.GroupRoles.Add(hostRole);

        var hostMembership = new GroupMember(club.Id, hostUserId, hostRole.Id, status: ClubMemberStatus.ACTIVE);
        dbContext.GroupMembers.Add(hostMembership);

        var session = new BadmintonSession(
            groupId: club.Id,
            title: "Buổi sinh hoạt cuối tuần",
            startTime: DateTime.UtcNow.AddHours(3),
            endTime: DateTime.UtcNow.AddHours(5),
            courtLocation: "Sân Cầu Lông Cầu Giấy",
            courtCount: 2,
            targetPlayerPerCourt: 6,
            maxPlayers: 12,
            deadlineBooking: DateTime.UtcNow.AddHours(2),
            status: SessionStatus.OPEN
        );
        dbContext.BadmintonSessions.Add(session);

        var hostBooking = new SessionBooking(session.Id, hostUserId, status: BookingStatus.CONFIRMED);
        dbContext.SessionBookings.Add(hostBooking);

        var recruitment = new GuestRecruitment(
            sessionId: session.Id,
            slotsNeeded: 3,
            minSkillLevel: SkillLevel.TBY,
            sharedCost: 50000m,
            scanRadiusKm: 5.0m,
            status: RecruitmentStatus.ACTIVE
        );
        dbContext.GuestRecruitments.Add(recruitment);

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var handler = new ExploreRecruitmentsQueryHandler(recruitmentRepo, clubMemberRepo, currentUserService);
        var query = new ExploreRecruitmentsQuery(null, null, 10, null);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Id.Should().Be(recruitment.Id);
    }

    [Fact]
    public async Task Handle_WhenRegularUserHasActiveBooking_ShouldSkipRecruitment()
    {
        using var dbContext = TestDbContextFactory.Create();
        var recruitmentRepo = new RecruitmentRepository(dbContext);
        var clubMemberRepo = new ClubMemberRepository(dbContext);

        var regularUserId = Guid.NewGuid();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(regularUserId);

        var club = new ClubGroup(groupName: "CLB Test Member", code: "CLB02");
        dbContext.ClubGroups.Add(club);

        var session = new BadmintonSession(
            groupId: club.Id,
            title: "Buổi giao lưu",
            startTime: DateTime.UtcNow.AddHours(2),
            endTime: DateTime.UtcNow.AddHours(4),
            courtLocation: "Sân Cầu Lông Kỳ Hòa",
            courtCount: 1,
            targetPlayerPerCourt: 6,
            maxPlayers: 6,
            deadlineBooking: DateTime.UtcNow.AddHours(1),
            status: SessionStatus.OPEN
        );
        dbContext.BadmintonSessions.Add(session);

        var userBooking = new SessionBooking(session.Id, regularUserId, status: BookingStatus.CONFIRMED);
        dbContext.SessionBookings.Add(userBooking);

        var recruitment = new GuestRecruitment(
            sessionId: session.Id,
            slotsNeeded: 2,
            minSkillLevel: SkillLevel.NB,
            sharedCost: 40000m,
            scanRadiusKm: 5.0m,
            status: RecruitmentStatus.ACTIVE
        );
        dbContext.GuestRecruitments.Add(recruitment);

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var handler = new ExploreRecruitmentsQueryHandler(recruitmentRepo, clubMemberRepo, currentUserService);
        var query = new ExploreRecruitmentsQuery(null, null, 10, null);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
