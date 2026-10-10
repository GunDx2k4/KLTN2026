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
            m => m.GroupId == club.Id && m.UserId == userId.Value,
            cancellationToken
        );

        if (existingMember is not null)
        {
            return Result.Failure<Guid>(Error.Conflict("ClubMember.AlreadyJoined", "Bạn đã là thành viên của câu lạc bộ này."));
        }

        var roleId = request.RoleId ?? club.Roles.FirstOrDefault(r => r.IsDefault)?.Id ?? club.Roles.FirstOrDefault()?.Id ?? Guid.Empty;

        var newMember = new GroupMember(
            groupId: club.Id,
            userId: userId.Value,
            roleId: roleId,
            memberType: "MONTHLY",
            monthlyExpiryDate: null,
            status: ClubMemberStatus.ACTIVE,
            nickname: request.Nickname
        );

        clubMemberRepository.Add(newMember);

        return Result.Success(newMember.Id);
    }
}
