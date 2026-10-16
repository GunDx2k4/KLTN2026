using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Join;

public sealed class JoinClubCommandHandler(
    IClubRepository clubRepository,
    IClubMemberRepository clubMemberRepository,
    IJoinRequestRepository joinRequestRepository,
    ICurrentUserService currentUserService) : ICommandHandler<JoinClubCommand, Guid>
{
    public async Task<Result<Guid>> Handle(JoinClubCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<Guid>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập để tham gia câu lạc bộ."));
        }

        var normalizedCode = request.ClubCode.Trim().ToUpperInvariant();
        var club = await clubRepository.FindSingleAsync(
            c => c.Code == normalizedCode,
            cancellationToken,
            c => c.Roles
        );

        if (club is null)
        {
            return Result.Failure<Guid>(Error.NotFound("Club.NotFound", $"Không tìm thấy câu lạc bộ với mã '{request.ClubCode}'."));
        }

        if (!club.IsActive)
        {
            return Result.Failure<Guid>(Error.Validation("Club.Inactive", "Câu lạc bộ hiện đang tạm ngừng hoạt động."));
        }

        var existingMember = await clubMemberRepository.FindSingleAsync(
            m => m.GroupId == club.Id && m.UserId == userId.Value && m.Status == ClubMemberStatus.ACTIVE,
            cancellationToken
        );

        if (existingMember is not null)
        {
            return Result.Failure<Guid>(Error.Conflict("ClubMember.AlreadyJoined", "Bạn đã là thành viên của câu lạc bộ này."));
        }

        var existingRequest = await joinRequestRepository.FindSingleAsync(
            r => r.GroupId == club.Id && r.UserId == userId.Value && r.Status == JoinRequestStatus.PENDING,
            cancellationToken
        );

        if (existingRequest is not null)
        {
            return Result.Failure<Guid>(Error.Conflict("JoinRequest.AlreadyPending", "Bạn đã gửi yêu cầu gia nhập câu lạc bộ này và đang chờ Ban quản trị duyệt."));
        }

        var intro = !string.IsNullOrWhiteSpace(request.Nickname)
            ? $"Biệt danh: {request.Nickname}"
            : $"Xin gia nhập nhóm bằng mã {normalizedCode}";

        var newJoinRequest = new JoinRequest(
            groupId: club.Id,
            userId: userId.Value,
            introduction: intro,
            status: JoinRequestStatus.PENDING
        );

        joinRequestRepository.Add(newJoinRequest);

        return Result.Success(newJoinRequest.Id);
    }
}
