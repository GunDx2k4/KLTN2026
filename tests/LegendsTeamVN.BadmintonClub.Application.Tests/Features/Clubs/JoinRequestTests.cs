using System.Linq.Expressions;
using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Requests;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.JoinRequests.Create;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Clubs;

public class JoinRequestTests
{
    private readonly IClubRepository _clubRepository = Substitute.For<IClubRepository>();
    private readonly IClubMemberRepository _clubMemberRepository = Substitute.For<IClubMemberRepository>();
    private readonly IJoinRequestRepository _joinRequestRepository = Substitute.For<IJoinRequestRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly CreateJoinRequestCommandHandler _handler;

    public JoinRequestTests()
    {
        _handler = new CreateJoinRequestCommandHandler(
            _clubRepository,
            _clubMemberRepository,
            _joinRequestRepository,
            _currentUserService);
    }

    [Fact]
    public async Task Handle_WhenPendingRequestAlreadyExists_ShouldReturnConflictError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var clubId = Guid.NewGuid();

        _currentUserService.UserId.Returns(userId);

        var club = new ClubGroup("CLB Cầu Lông Hà Nội", isActive: true);
        _clubRepository.FindByIdAsync(clubId, Arg.Any<CancellationToken>())
            .Returns(club);

        _clubMemberRepository.FindSingleAsync(
            Arg.Any<Expression<Func<GroupMember, bool>>>(),
            Arg.Any<CancellationToken>())
            .Returns((GroupMember?)null);

        // Đã có đơn PENDING trước đó
        var existingPendingRequest = new JoinRequest(clubId, userId, "Xin chào CLB", JoinRequestStatus.PENDING);
        _joinRequestRepository.FindSingleAsync(
            Arg.Any<Expression<Func<JoinRequest, bool>>>(),
            Arg.Any<CancellationToken>())
            .Returns(existingPendingRequest);

        var command = new CreateJoinRequestCommand(clubId, "Gửi lại đơn");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert: Chặn gửi đơn trùng lặp
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("JoinRequest.AlreadyPending");
        result.Error.Message.Should().Contain("Bạn đã gửi yêu cầu gia nhập câu lạc bộ này và đang chờ xét duyệt");
        _joinRequestRepository.DidNotReceive().Add(Arg.Any<JoinRequest>());
    }

    [Fact]
    public async Task Handle_WhenUserAlreadyMember_ShouldReturnConflictError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var clubId = Guid.NewGuid();

        _currentUserService.UserId.Returns(userId);

        var club = new ClubGroup("CLB Cầu Lông Hà Nội", isActive: true);
        _clubRepository.FindByIdAsync(clubId, Arg.Any<CancellationToken>())
            .Returns(club);

        var member = new GroupMember(clubId, userId, Guid.NewGuid(), status: ClubMemberStatus.ACTIVE);
        _clubMemberRepository.FindSingleAsync(
            Arg.Any<Expression<Func<GroupMember, bool>>>(),
            Arg.Any<CancellationToken>())
            .Returns(member);

        var command = new CreateJoinRequestCommand(clubId, "Xin tham gia");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ClubMember.AlreadyJoined");
    }

    [Fact]
    public async Task Handle_WhenNoPriorRequestAndNotMember_ShouldCreatePendingJoinRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var clubId = Guid.NewGuid();

        _currentUserService.UserId.Returns(userId);

        var club = new ClubGroup("CLB Cầu Lông Hà Nội", isActive: true);
        _clubRepository.FindByIdAsync(clubId, Arg.Any<CancellationToken>())
            .Returns(club);

        _clubMemberRepository.FindSingleAsync(
            Arg.Any<Expression<Func<GroupMember, bool>>>(),
            Arg.Any<CancellationToken>())
            .Returns((GroupMember?)null);

        _joinRequestRepository.FindSingleAsync(
            Arg.Any<Expression<Func<JoinRequest, bool>>>(),
            Arg.Any<CancellationToken>())
            .Returns((JoinRequest?)null);

        var command = new CreateJoinRequestCommand(clubId, "Em xin vào nhóm đánh thứ 3-5-7");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _joinRequestRepository.Received(1).Add(Arg.Is<JoinRequest>(r =>
            r.GroupId == clubId &&
            r.UserId == userId &&
            r.Status == JoinRequestStatus.PENDING &&
            r.Introduction == "Em xin vào nhóm đánh thứ 3-5-7"));
    }
}
