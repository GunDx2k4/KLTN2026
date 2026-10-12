using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.JoinRequests.Create;

public sealed class CreateJoinRequestCommandHandler(
    IClubRepository clubRepository,
    IClubMemberRepository clubMemberRepository,
    IJoinRequestRepository joinRequestRepository,
    ICurrentUserService currentUserService) : ICommandHandler<CreateJoinRequestCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateJoinRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<Guid>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập để gửi yêu cầu gia nhập."));
        }

        var club = await clubRepository.FindByIdAsync(request.ClubId, cancellationToken);
        if (club is null)
        {
            return Result.Failure<Guid>(Error.NotFound("Club.NotFound", "Không tìm thấy câu lạc bộ."));
        }

        if (!club.IsActive)
        {
            return Result.Failure<Guid>(Error.Validation("Club.Inactive", "Câu lạc bộ hiện đang tạm ngừng hoạt động."));
        }

        var isAlreadyMember = await clubMemberRepository.FindSingleAsync(
            m => m.GroupId == request.ClubId && m.UserId == userId.Value,
            cancellationToken
        );

        if (isAlreadyMember is not null)
        {
            return Result.Failure<Guid>(Error.Conflict("ClubMember.AlreadyJoined", "Bạn đã là thành viên của câu lạc bộ này."));
        }

        var pendingRequest = await joinRequestRepository.FindSingleAsync(
            r => r.GroupId == request.ClubId && r.UserId == userId.Value && r.Status == JoinRequestStatus.PENDING,
            cancellationToken
        );

        if (pendingRequest is not null)
        {
            return Result.Failure<Guid>(Error.Conflict("JoinRequest.AlreadyPending", "Bạn đã gửi yêu cầu gia nhập câu lạc bộ này và đang chờ xét duyệt."));
        }

        var newJoinRequest = new JoinRequest(
            groupId: request.ClubId,
            userId: userId.Value,
            introduction: request.Introduction,
            status: JoinRequestStatus.PENDING
        );

        joinRequestRepository.Add(newJoinRequest);

        return Result.Success(newJoinRequest.Id);
    }
}
