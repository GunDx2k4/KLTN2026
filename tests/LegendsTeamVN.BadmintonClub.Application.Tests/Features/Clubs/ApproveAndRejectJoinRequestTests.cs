using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.JoinRequests.Approve;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.JoinRequests.Reject;
using LegendsTeamVN.BadmintonClub.Application.Tests.Common;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Clubs;

public class ApproveAndRejectJoinRequestTests
{
    [Fact]
    public async Task Handle_WhenHostApprovesPendingRequest_ShouldUpdateStatusToApprovedAndCreateClubMember()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var clubRepo = new ClubRepository(dbContext);
        var memberRepo = new ClubMemberRepository(dbContext);
        var joinRepo = new JoinRequestRepository(dbContext);
        var currentUserService = Substitute.For<ICurrentUserService>();

        var hostUserId = Guid.NewGuid();
        currentUserService.UserId.Returns(hostUserId);

        var club = new ClubGroup("CLB Cầu Lông Ba Đình", "CLB-BD", "CLB phong trào Ba Đình", "Sân Quần Ngựa");
        var defaultRole = new GroupRole(club.Id, "Thành viên", "Thành viên chính thức", isDefault: true);
        club.Roles.Add(defaultRole);
        dbContext.ClubGroups.Add(club);

        var requestingUserId = Guid.NewGuid();
        var joinRequest = new JoinRequest(club.Id, requestingUserId, "Em xin tham gia nhóm sinh hoạt tối Thứ 3 - 5 - 7");
        dbContext.JoinRequests.Add(joinRequest);

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var handler = new ApproveJoinRequestCommandHandler(
            clubRepo,
            memberRepo,
            joinRepo,
            currentUserService);

        var command = new ApproveJoinRequestCommand(club.Id, joinRequest.Id, RoleId: null, MemberType: "MONTHLY");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        var updatedRequest = dbContext.JoinRequests.First(r => r.Id == joinRequest.Id);
        updatedRequest.Status.Should().Be(JoinRequestStatus.APPROVED);
        updatedRequest.ApprovedBy.Should().Be(hostUserId);
        updatedRequest.ReviewedAt.Should().NotBeNull();

        var newMember = dbContext.GroupMembers.FirstOrDefault(m => m.GroupId == club.Id && m.UserId == requestingUserId);
        newMember.Should().NotBeNull();
        newMember!.Status.Should().Be(ClubMemberStatus.ACTIVE);
        newMember.RoleId.Should().Be(defaultRole.Id);
        newMember.MemberType.Should().Be("MONTHLY");
    }

    [Fact]
    public async Task Handle_WhenHostRejectsPendingRequest_ShouldUpdateStatusToRejectedWithoutCreatingMember()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var joinRepo = new JoinRequestRepository(dbContext);
        var currentUserService = Substitute.For<ICurrentUserService>();

        var hostUserId = Guid.NewGuid();
        currentUserService.UserId.Returns(hostUserId);

        var clubId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();
        var joinRequest = new JoinRequest(clubId, requestingUserId, "Xin vào nhóm");
        dbContext.JoinRequests.Add(joinRequest);

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var handler = new RejectJoinRequestCommandHandler(joinRepo, currentUserService);
        var command = new RejectJoinRequestCommand(clubId, joinRequest.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        var updatedRequest = dbContext.JoinRequests.First(r => r.Id == joinRequest.Id);
        updatedRequest.Status.Should().Be(JoinRequestStatus.REJECTED);
        updatedRequest.ApprovedBy.Should().Be(hostUserId);

        var memberExists = dbContext.GroupMembers.Any(m => m.GroupId == clubId && m.UserId == requestingUserId);
        memberExists.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenRequestAlreadyProcessed_ShouldReturnAlreadyProcessedError()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var clubRepo = new ClubRepository(dbContext);
        var memberRepo = new ClubMemberRepository(dbContext);
        var joinRepo = new JoinRequestRepository(dbContext);
        var currentUserService = Substitute.For<ICurrentUserService>();

        var hostUserId = Guid.NewGuid();
        currentUserService.UserId.Returns(hostUserId);

        var clubId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();
        var joinRequest = new JoinRequest(clubId, requestingUserId, "Xin vào nhóm");
        joinRequest.Approve(hostUserId); // Already approved!
        dbContext.JoinRequests.Add(joinRequest);

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var handler = new ApproveJoinRequestCommandHandler(
            clubRepo,
            memberRepo,
            joinRepo,
            currentUserService);

        var command = new ApproveJoinRequestCommand(clubId, joinRequest.Id, RoleId: null, MemberType: "MONTHLY");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("JoinRequest.AlreadyProcessed");
    }
}
