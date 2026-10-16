using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.JoinRequests.Approve;

public sealed class ApproveJoinRequestCommandHandler(
    IClubRepository clubRepository,
    IClubMemberRepository clubMemberRepository,
    IJoinRequestRepository joinRequestRepository,
    ICurrentUserService currentUserService) : ICommandHandler<ApproveJoinRequestCommand, Guid>
{
    public async Task<Result<Guid>> Handle(ApproveJoinRequestCommand request, CancellationToken cancellationToken)
    {
        var approverId = currentUserService.UserId;
        if (!approverId.HasValue)
        {
            return Result.Failure<Guid>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        }

        var joinRequest = await joinRequestRepository.FindSingleAsync(
            r => r.Id == request.RequestId && r.GroupId == request.ClubId,
            cancellationToken
        );

        if (joinRequest is null)
        {
            return Result.Failure<Guid>(Error.NotFound("JoinRequest.NotFound", "Không tìm thấy yêu cầu gia nhập trong câu lạc bộ này."));
        }

        if (joinRequest.Status != JoinRequestStatus.PENDING)
        {
            return Result.Failure<Guid>(Error.Validation("JoinRequest.AlreadyProcessed", "Yêu cầu gia nhập này đã được xử lý trước đó."));
        }

        var club = await clubRepository.FindByIdAsync(request.ClubId, cancellationToken, c => c.Roles);
        if (club is null)
        {
            return Result.Failure<Guid>(Error.NotFound("Club.NotFound", "Không tìm thấy câu lạc bộ."));
        }

        var defaultRole = club.GetDefaultRole() ?? club.Roles.FirstOrDefault(r => r.IsDefault) ?? club.Roles.FirstOrDefault(r => r.RoleName == "Thành viên");
        var targetRoleId = request.RoleId.HasValue && club.FindRole(request.RoleId.Value) is not null
            ? request.RoleId.Value
            : defaultRole?.Id ?? club.Roles.FirstOrDefault(r => !r.IsHostRole)?.Id ?? club.Roles.FirstOrDefault()?.Id ?? Guid.Empty;

        var existingMember = await clubMemberRepository.FindSingleAsync(
            m => m.GroupId == request.ClubId && m.UserId == joinRequest.UserId,
            cancellationToken
        );

        if (existingMember is not null)
        {
            joinRequest.Approve(approverId.Value);
            return Result.Failure<Guid>(Error.Conflict("ClubMember.AlreadyJoined", "Người dùng này đã là thành viên của câu lạc bộ."));
        }

        joinRequest.Approve(approverId.Value);

        var newMember = new GroupMember(
            groupId: request.ClubId,
            userId: joinRequest.UserId,
            roleId: targetRoleId,
            memberType: string.IsNullOrWhiteSpace(request.MemberType) ? "MONTHLY" : request.MemberType,
            monthlyExpiryDate: null,
            status: ClubMemberStatus.ACTIVE
        );

        clubMemberRepository.Add(newMember);

        return Result.Success(newMember.Id);
    }
}
