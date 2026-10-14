using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.Core.Domain.Entities;
using NetArchTest.Rules;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Architecture.Tests;

public class DomainTests
{
    [Fact]
    public void DomainLayer_ShouldNotHaveDependencyOn_ApplicationLayer()
    {
        var result = Types.InAssembly(Domain.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOn("LegendsTeamVN.BadmintonClub.Application")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void DomainLayer_ShouldNotHaveDependencyOn_InfrastructureLayer()
    {
        var result = Types.InAssembly(Domain.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOn("LegendsTeamVN.BadmintonClub.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void DomainLayer_ShouldNotHaveDependencyOn_PersistenceLayer()
    {
        var result = Types.InAssembly(Domain.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOn("LegendsTeamVN.BadmintonClub.Persistence")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void DomainLayer_ShouldNotHaveDependencyOn_PresentationLayer()
    {
        var result = Types.InAssembly(Domain.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOn("LegendsTeamVN.BadmintonClub.Presentation")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Entities_ShouldHave_ParameterlessConstructor()
    {
        var entityTypes = Types.InAssembly(Domain.AssemblyReference.Assembly)
            .That()
            .Inherit(typeof(Entity<>))
            .GetTypes();

        var failingTypes = new List<Type>();
        
        foreach (var type in entityTypes)
        {
            var constructors = type.GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (!constructors.Any(c => c.GetParameters().Length == 0))
            {
                failingTypes.Add(type);
            }
        }

        failingTypes.Should().BeEmpty();
    }

    [Fact]
    public void DomainEvents_Should_BeSealed()
    {
        var result = Types.InAssembly(Domain.AssemblyReference.Assembly)
            .That()
            .ImplementInterface(typeof(LegendsTeamVN.Core.Domain.Events.IDomainEvent))
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void DomainEvents_ShouldHave_DomainEventPostfix()
    {
        var result = Types.InAssembly(Domain.AssemblyReference.Assembly)
            .That()
            .ImplementInterface(typeof(LegendsTeamVN.Core.Domain.Events.IDomainEvent))
            .Should()
            .HaveNameEndingWith("DomainEvent")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Entities_ShouldHave_PrivateSetters()
    {
        var entityTypes = Types.InAssembly(Domain.AssemblyReference.Assembly)
            .That()
            .Inherit(typeof(Entity<>))
            .GetTypes();

        var failingTypes = new List<Type>();
        
        foreach (var type in entityTypes)
        {
            var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (properties.Any(p => p.CanWrite && p.SetMethod != null && p.SetMethod.IsPublic))
            {
                failingTypes.Add(type);
            }
        }

        failingTypes.Should().BeEmpty("Entities should not have public setters to enforce encapsulation.");
    }

    [Fact]
    public void FR03_UserCanHoldIndependentRoles_AcrossMultipleClubs()
    {
        var userId = Guid.NewGuid();
        var clubAId = Guid.NewGuid();
        var clubBId = Guid.NewGuid();
        var clubCId = Guid.NewGuid();

        var roleAId = Guid.NewGuid();
        var roleBId = Guid.NewGuid();
        var roleCId = Guid.NewGuid();

        // 1 User simultaneously participates in 3 clubs with independent roles
        var membershipA = new GroupMember(clubAId, userId, roleAId, status: Domain.Enums.ClubMemberStatus.ACTIVE);
        var membershipB = new GroupMember(clubBId, userId, roleBId, status: Domain.Enums.ClubMemberStatus.ACTIVE);
        var membershipC = new GroupMember(clubCId, userId, roleCId, status: Domain.Enums.ClubMemberStatus.ACTIVE);

        membershipA.RoleId.Should().Be(roleAId);
        membershipB.RoleId.Should().Be(roleBId);
        membershipC.RoleId.Should().Be(roleCId);

        membershipA.GroupId.Should().Be(clubAId);
        membershipB.GroupId.Should().Be(clubBId);
        membershipC.GroupId.Should().Be(clubCId);

        membershipA.UserId.Should().Be(userId);
        membershipB.UserId.Should().Be(userId);
        membershipC.UserId.Should().Be(userId);
    }

    [Fact]
    public void JoinRequest_ApproveAndReject_ShouldUpdateStatusAndReviewer()
    {
        var clubId = Guid.NewGuid();
        var applicantId = Guid.NewGuid();
        var approverId = Guid.NewGuid();

        var request = new JoinRequest(clubId, applicantId, "Xin gia nhập nhóm");
        request.Status.Should().Be(Domain.Enums.JoinRequestStatus.PENDING);

        request.Approve(approverId);
        request.Status.Should().Be(Domain.Enums.JoinRequestStatus.APPROVED);
        request.ApprovedBy.Should().Be(approverId);
        request.ReviewedAt.Should().NotBeNull();

        var request2 = new JoinRequest(clubId, applicantId, "Xin gia nhập nhóm 2");
        request2.Reject(approverId);
        request2.Status.Should().Be(Domain.Enums.JoinRequestStatus.REJECTED);
        request2.ApprovedBy.Should().Be(approverId);
    }

    [Fact]
    public void FR13_CreateBadmintonSession_ShouldInitializeCorrectly()
    {
        var groupId = Guid.NewGuid();
        var startTime = DateTime.UtcNow.AddDays(1);
        var endTime = startTime.AddHours(3);
        var session = new BadmintonSession(
            groupId: groupId,
            title: "Buổi sinh hoạt cuối tuần",
            startTime: startTime,
            endTime: endTime,
            courtLocation: "Sân Kỳ Hòa",
            courtCount: 2,
            targetPlayerPerCourt: 6,
            maxPlayers: 12
        );

        session.GroupId.Should().Be(groupId);
        session.Title.Should().Be("Buổi sinh hoạt cuối tuần");
        session.MaxPlayers.Should().Be(12);
        session.Status.Should().Be(Domain.Enums.SessionStatus.OPEN);
        session.DeadlineBooking.Should().BeBefore(startTime);
    }

    [Fact]
    public void FR08_FR10_SessionBooking_QueueAndCheckIn_ShouldWorkAsExpected()
    {
        var sessionId = Guid.NewGuid();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        // User 1: Confirmed
        var booking1 = new SessionBooking(sessionId, user1, status: Domain.Enums.BookingStatus.CONFIRMED, queueOrder: 0);
        booking1.Status.Should().Be(Domain.Enums.BookingStatus.CONFIRMED);
        booking1.QueueOrder.Should().Be(0);

        // User 2: Waiting in queue (Order 1)
        var booking2 = new SessionBooking(sessionId, user2, status: Domain.Enums.BookingStatus.WAITING, queueOrder: 1);
        booking2.Status.Should().Be(Domain.Enums.BookingStatus.WAITING);
        booking2.QueueOrder.Should().Be(1);

        // Auto-promote booking 2 when booking 1 is absent
        booking1.UpdateStatus(Domain.Enums.BookingStatus.ABSENT);
        booking2.UpdateStatus(Domain.Enums.BookingStatus.CONFIRMED);
        booking2.UpdateQueueOrder(0);

        booking1.Status.Should().Be(Domain.Enums.BookingStatus.ABSENT);
        booking2.Status.Should().Be(Domain.Enums.BookingStatus.CONFIRMED);
        booking2.QueueOrder.Should().Be(0);

        // Check in
        booking2.CheckIn();
        booking2.Status.Should().Be(Domain.Enums.BookingStatus.CHECKED_IN);
        booking2.CheckinAt.Should().NotBeNull();
    }

    [Fact]
    public void FR11_FR12_GuestRecruitment_Transitions_ShouldWorkCorrectly()
    {
        var sessionId = Guid.NewGuid();
        var recruitment = new GuestRecruitment(sessionId, slotsNeeded: 2, sharedCost: 50000);

        recruitment.Status.Should().Be(Domain.Enums.RecruitmentStatus.ACTIVE);
        recruitment.SlotsNeeded.Should().Be(2);
        recruitment.SharedCost.Should().Be(50000);

        recruitment.UpdateStatus(Domain.Enums.RecruitmentStatus.FILLED);
        recruitment.Status.Should().Be(Domain.Enums.RecruitmentStatus.FILLED);
    }

    [Fact]
    public void FR05_AppUser_UpdateProfile_ShouldUpdateFieldsCorrectly()
    {
        var user = AppUser.CreateStandard(Guid.NewGuid(), "Nguyễn Văn A", "a@test.com", "0901234567", 1, Domain.Enums.SkillLevel.NB);
        user.SkillLevel.Should().Be(Domain.Enums.SkillLevel.NB);

        user.UpdateProfile("Nguyễn Văn A Updated", 1, "https://avatar.png", Domain.Enums.SkillLevel.TB, isOpenForGuest: true);

        user.FullName.Should().Be("Nguyễn Văn A Updated");
        user.AvatarUrl.Should().Be("https://avatar.png");
        user.SkillLevel.Should().Be(Domain.Enums.SkillLevel.TB);
        user.IsOpenForGuest.Should().BeTrue();
    }

    [Fact]
    public void FR07_ClubGroup_UpdateInfoAndSchedule_ShouldWorkCorrectly()
    {
        var club = new ClubGroup("CLB Test", description: "Mô tả", avatarUrl: "https://avatar.png");
        club.FixedVenueName.Should().BeNull();

        club.UpdateInfo("CLB Test 2", "Mô tả 2", "https://new.png", "Sân Kỳ Hòa", "238 Đường 3/2", 10.77m, 106.69m, Domain.Enums.SkillLevel.TB, Domain.Enums.SkillLevel.K);
        club.SetRecruiting(true);

        club.GroupName.Should().Be("CLB Test 2");
        club.FixedVenueName.Should().Be("Sân Kỳ Hòa");
        club.FixedAddress.Should().Be("238 Đường 3/2");
        club.Lat.Should().Be(10.77m);
        club.Lng.Should().Be(106.69m);
        club.MinSkillLevel.Should().Be(Domain.Enums.SkillLevel.TB);
        club.MaxSkillLevel.Should().Be(Domain.Enums.SkillLevel.K);
        club.IsRecruiting.Should().BeTrue();

        var schedule = new GroupSchedule(club.Id, 2, new TimeOnly(18, 0), new TimeOnly(21, 0));
        schedule.DayOfWeek.Should().Be(2);
        schedule.StartTime.Should().Be(new TimeOnly(18, 0));
        schedule.EndTime.Should().Be(new TimeOnly(21, 0));
        schedule.IsActive.Should().BeTrue();
    }
}

