using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Roles.Create;

public sealed class CreateClubRoleCommandHandler(
    IClubRepository clubRepository) : ICommandHandler<CreateClubRoleCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateClubRoleCommand request, CancellationToken cancellationToken)
    {
        var club = await clubRepository.FindByIdAsync(request.ClubId, cancellationToken, c => c.Roles);
        if (club is null)
        {
            return Result.Failure<Guid>(Error.NotFound("Club.NotFound", "Không tìm thấy câu lạc bộ."));
        }

        if (club.Roles.Any(r => r.RoleName.Equals(request.RoleName.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure<Guid>(Error.Conflict("ClubRole.AlreadyExists", $"Vai trò '{request.RoleName}' đã tồn tại trong câu lạc bộ."));
        }

        var customRole = club.AddCustomRole(
            roleName: request.RoleName.Trim(),
            description: request.Description,
            permissionIds: request.PermissionIds ?? [],
            isDefault: request.IsDefault
        );

        return Result.Success(customRole.Id);
    }
}
