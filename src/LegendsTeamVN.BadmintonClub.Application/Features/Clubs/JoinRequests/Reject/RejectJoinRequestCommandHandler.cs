using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.JoinRequests.Reject;

public sealed class RejectJoinRequestCommandHandler(
    IJoinRequestRepository joinRequestRepository,
    ICurrentUserService currentUserService) : ICommandHandler<RejectJoinRequestCommand, bool>
{
    public async Task<Result<bool>> Handle(RejectJoinRequestCommand request, CancellationToken cancellationToken)
    {
        var reviewerId = currentUserService.UserId;
        if (!reviewerId.HasValue)
        {
            return Result.Failure<bool>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        }

        var joinRequest = await joinRequestRepository.FindSingleAsync(
            r => r.Id == request.RequestId && r.GroupId == request.ClubId,
            cancellationToken
        );

        if (joinRequest is null)
        {
            return Result.Failure<bool>(Error.NotFound("JoinRequest.NotFound", "Không tìm thấy yêu cầu gia nhập trong câu lạc bộ này."));
        }

        if (joinRequest.Status != JoinRequestStatus.PENDING)
        {
            return Result.Failure<bool>(Error.Validation("JoinRequest.AlreadyProcessed", "Yêu cầu gia nhập này đã được xử lý trước đó."));
        }

        joinRequest.Reject(reviewerId.Value);

        return Result.Success(true);
    }
}
