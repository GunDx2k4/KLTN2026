using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Roles.Delete;

public sealed class DeleteClubRoleCommandHandler(
    IClubRepository clubRepository,
    IClubMemberRepository clubMemberRepository) : ICommandHandler<DeleteClubRoleCommand, bool>
{
    public async Task<Result<bool>> Handle(DeleteClubRoleCommand request, CancellationToken cancellationToken)
    {
        var club = await clubRepository.FindByIdAsync(request.ClubId, cancellationToken, c => c.Roles);
        if (club is null)
        {
            return Result.Failure<bool>(Error.NotFound("Club.NotFound", "Không tìm thấy câu lạc bộ."));
        }

        var role = club.FindRole(request.RoleId);
        if (role is null)
        {
            return Result.Failure<bool>(Error.NotFound("ClubRole.NotFound", "Không tìm thấy vai trò trong câu lạc bộ."));
        }

        if (!role.CanBeDeleted(out var reason))
        {
            return Result.Failure<bool>(Error.Validation("ClubRole.CannotDelete", reason));
        }

        var isRoleInUse = await clubMemberRepository
            .FindAll(m => m.GroupId == request.ClubId && m.RoleId == request.RoleId)
            .AnyAsync(cancellationToken);

        if (isRoleInUse)
        {
            return Result.Failure<bool>(Error.Conflict("ClubRole.InUse", "Không thể xóa vai trò đang được gán cho thành viên trong câu lạc bộ."));
        }

        club.RemoveRole(request.RoleId);

        return Result.Success(true);
    }
}
