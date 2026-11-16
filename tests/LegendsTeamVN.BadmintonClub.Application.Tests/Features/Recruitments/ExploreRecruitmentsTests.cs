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

        var handler = new ExploreRecruitmentsQueryHandler(recruitmentRepo, currentUserService);
        var query = new ExploreRecruitmentsQuery(null, null, 10, null);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Id.Should().Be(recruitment.Id);
    }

    [Fact]
    public async Task Handle_WhenRegularUserHasBooking_ShouldReturnRecruitmentWithMyBookingStatus()
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

        var userBooking = new SessionBooking(session.Id, regularUserId, status: BookingStatus.WAITING);
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

        var handler = new ExploreRecruitmentsQueryHandler(recruitmentRepo, currentUserService);
        var query = new ExploreRecruitmentsQuery(null, null, 10, null);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Id.Should().Be(recruitment.Id);
        result.Value[0].MyBookingStatus.Should().Be("WAITING");
    }

    [Fact]
    public async Task Handle_WhenRecruitmentIsFilled_AndUserHasWaitingBooking_ShouldReturnRecruitment()
    {
        using var dbContext = TestDbContextFactory.Create();
        var recruitmentRepo = new RecruitmentRepository(dbContext);
        var clubMemberRepo = new ClubMemberRepository(dbContext);

        var userId = Guid.NewGuid();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(userId);

        var club = new ClubGroup(groupName: "CLB Test Filled", code: "CLB03");
        dbContext.ClubGroups.Add(club);

        var session = new BadmintonSession(
            groupId: club.Id,
            title: "Buổi giao lưu đã đủ người",
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

        var recruitment = new GuestRecruitment(
            sessionId: session.Id,
            slotsNeeded: 2,
            minSkillLevel: SkillLevel.NB,
            sharedCost: 40000m,
            scanRadiusKm: 5.0m,
            status: RecruitmentStatus.FILLED
        );
        dbContext.GuestRecruitments.Add(recruitment);

        var userBooking = new SessionBooking(session.Id, userId, recruitmentId: recruitment.Id, status: BookingStatus.WAITING);
        dbContext.SessionBookings.Add(userBooking);

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var handler = new ExploreRecruitmentsQueryHandler(recruitmentRepo, currentUserService);
        var query = new ExploreRecruitmentsQuery(null, null, 10, null);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Id.Should().Be(recruitment.Id);
        result.Value[0].MyBookingStatus.Should().Be("WAITING");
    }

    [Fact]
    public async Task Handle_WhenSessionIsPlaying_AndUserIsConfirmed_ShouldReturnRecruitment()
    {
        using var dbContext = TestDbContextFactory.Create();
        var recruitmentRepo = new RecruitmentRepository(dbContext);
        var clubMemberRepo = new ClubMemberRepository(dbContext);

        var userId = Guid.NewGuid();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(userId);

        var club = new ClubGroup(groupName: "CLB Test Playing", code: "CLB04");
        dbContext.ClubGroups.Add(club);

        var session = new BadmintonSession(
            groupId: club.Id,
            title: "Buổi giao lưu đang chơi",
            startTime: DateTime.UtcNow.AddHours(-1),
            endTime: DateTime.UtcNow.AddHours(2),
            courtLocation: "Sân Cầu Lông Kỳ Hòa",
            courtCount: 1,
            targetPlayerPerCourt: 6,
            maxPlayers: 6,
            deadlineBooking: DateTime.UtcNow.AddHours(-2),
            status: SessionStatus.PLAYING
        );
        dbContext.BadmintonSessions.Add(session);

        var recruitment = new GuestRecruitment(
            sessionId: session.Id,
            slotsNeeded: 2,
            minSkillLevel: SkillLevel.NB,
            sharedCost: 40000m,
            scanRadiusKm: 5.0m,
            status: RecruitmentStatus.FILLED
        );
        dbContext.GuestRecruitments.Add(recruitment);

        var userBooking = new SessionBooking(session.Id, userId, recruitmentId: recruitment.Id, status: BookingStatus.CONFIRMED);
        dbContext.SessionBookings.Add(userBooking);

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var handler = new ExploreRecruitmentsQueryHandler(recruitmentRepo, currentUserService);
        var query = new ExploreRecruitmentsQuery(null, null, 10, null);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Id.Should().Be(recruitment.Id);
        result.Value[0].MyBookingStatus.Should().Be("CONFIRMED");
    }

    [Fact]
    public async Task Handle_WhenRecruitmentIsFilled_AndUserHasNoBooking_ShouldNotReturn()
    {
        using var dbContext = TestDbContextFactory.Create();
        var recruitmentRepo = new RecruitmentRepository(dbContext);
        var clubMemberRepo = new ClubMemberRepository(dbContext);

        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(userId);

        var club = new ClubGroup(groupName: "CLB Test Filled Non Participant", code: "CLB05");
        dbContext.ClubGroups.Add(club);

        var session = new BadmintonSession(
            groupId: club.Id,
            title: "Buổi giao lưu đã đủ",
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

        var recruitment = new GuestRecruitment(
            sessionId: session.Id,
            slotsNeeded: 2,
            minSkillLevel: SkillLevel.NB,
            sharedCost: 40000m,
            scanRadiusKm: 5.0m,
            status: RecruitmentStatus.FILLED
        );
        dbContext.GuestRecruitments.Add(recruitment);

        // Another user has the booking, not the querying user
        var otherBooking = new SessionBooking(session.Id, otherUserId, recruitmentId: recruitment.Id, status: BookingStatus.CONFIRMED);
        dbContext.SessionBookings.Add(otherBooking);

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var handler = new ExploreRecruitmentsQueryHandler(recruitmentRepo, currentUserService);
        var query = new ExploreRecruitmentsQuery(null, null, 10, null);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenGpsDisabled_ShouldOrderByClubQualityScoreDescending()
    {
        using var dbContext = TestDbContextFactory.Create();
        var recruitmentRepo = new RecruitmentRepository(dbContext);

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(Guid.NewGuid());

        var clubLow = new ClubGroup(groupName: "CLB Low Score", code: "CLBLOW", groupQualityScore: 3.5m);
        var clubHigh = new ClubGroup(groupName: "CLB High Score", code: "CLBHIGH", groupQualityScore: 4.9m);
        dbContext.ClubGroups.AddRange(clubLow, clubHigh);

        var sessionLow = new BadmintonSession(
            groupId: clubLow.Id,
            title: "Buoi choi Low",
            startTime: DateTime.UtcNow.AddHours(2),
            endTime: DateTime.UtcNow.AddHours(4),
            courtLocation: "San A",
            courtCount: 1,
            targetPlayerPerCourt: 6,
            maxPlayers: 6,
            deadlineBooking: DateTime.UtcNow.AddHours(1),
            status: SessionStatus.OPEN
        );
        var sessionHigh = new BadmintonSession(
            groupId: clubHigh.Id,
            title: "Buoi choi High",
            startTime: DateTime.UtcNow.AddHours(2),
            endTime: DateTime.UtcNow.AddHours(4),
            courtLocation: "San B",
            courtCount: 1,
            targetPlayerPerCourt: 6,
            maxPlayers: 6,
            deadlineBooking: DateTime.UtcNow.AddHours(1),
            status: SessionStatus.OPEN
        );
        dbContext.BadmintonSessions.AddRange(sessionLow, sessionHigh);

        var recLow = new GuestRecruitment(sessionLow.Id, 2, SkillLevel.NB, 50000m, 5m, RecruitmentStatus.ACTIVE);
        var recHigh = new GuestRecruitment(sessionHigh.Id, 2, SkillLevel.NB, 50000m, 5m, RecruitmentStatus.ACTIVE);
        dbContext.GuestRecruitments.AddRange(recLow, recHigh);

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var handler = new ExploreRecruitmentsQueryHandler(recruitmentRepo, currentUserService);
        // GPS is null (disabled)
        var query = new ExploreRecruitmentsQuery(null, null, 10, null);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        // First one should be clubHigh (4.9m), second clubLow (3.5m)
        result.Value[0].ClubName.Should().Be("CLB High Score");
        result.Value[0].ClubQualityScore.Should().Be(4.9m);
        result.Value[1].ClubName.Should().Be("CLB Low Score");
        result.Value[1].ClubQualityScore.Should().Be(3.5m);
    }

    [Fact]
    public async Task Handle_WhenGpsEnabled_ShouldPrioritizeNearDistanceAndShowAllWithoutCutoff()
    {
        using var dbContext = TestDbContextFactory.Create();
        var recruitmentRepo = new RecruitmentRepository(dbContext);

        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns(Guid.NewGuid());

        // User is at 10.7700, 106.6600
        // Near club: 10.7710, 106.6610 (~0.15 km away)
        var clubNear = new ClubGroup(groupName: "CLB Near", code: "NEAR", groupQualityScore: 4.0m, lat: 10.7710m, lng: 106.6610m);
        // Far club: 10.9900, 106.8800 (> 30 km away, would previously be excluded by 10km cutoff)
        var clubFar = new ClubGroup(groupName: "CLB Far", code: "FAR", groupQualityScore: 5.0m, lat: 10.9900m, lng: 106.8800m);
        dbContext.ClubGroups.AddRange(clubNear, clubFar);

        var sessionNear = new BadmintonSession(
            groupId: clubNear.Id,
            title: "Buoi choi Near",
            startTime: DateTime.UtcNow.AddHours(2),
            endTime: DateTime.UtcNow.AddHours(4),
            courtLocation: "San Near",
            courtCount: 1,
            targetPlayerPerCourt: 6,
            maxPlayers: 6,
            deadlineBooking: DateTime.UtcNow.AddHours(1),
            status: SessionStatus.OPEN
        );
        var sessionFar = new BadmintonSession(
            groupId: clubFar.Id,
            title: "Buoi choi Far",
            startTime: DateTime.UtcNow.AddHours(2),
            endTime: DateTime.UtcNow.AddHours(4),
            courtLocation: "San Far",
            courtCount: 1,
            targetPlayerPerCourt: 6,
            maxPlayers: 6,
            deadlineBooking: DateTime.UtcNow.AddHours(1),
            status: SessionStatus.OPEN
        );
        dbContext.BadmintonSessions.AddRange(sessionNear, sessionFar);

        var recNear = new GuestRecruitment(sessionNear.Id, 2, SkillLevel.NB, 50000m, 5m, RecruitmentStatus.ACTIVE);
        var recFar = new GuestRecruitment(sessionFar.Id, 2, SkillLevel.NB, 50000m, 5m, RecruitmentStatus.ACTIVE);
        dbContext.GuestRecruitments.AddRange(recNear, recFar);

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var handler = new ExploreRecruitmentsQueryHandler(recruitmentRepo, currentUserService);
        var query = new ExploreRecruitmentsQuery(10.7700, 106.6600, 10, null);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // Both should be returned ("hien tat ca", not dropped)
        result.Value.Should().HaveCount(2);
        // Near club should be first
        result.Value[0].ClubName.Should().Be("CLB Near");
        result.Value[0].DistanceKm.Should().BeLessThan(1.0);
        result.Value[1].ClubName.Should().Be("CLB Far");
        result.Value[1].DistanceKm.Should().BeGreaterThan(20.0);
    }
}
