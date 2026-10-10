namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;

public interface IGroupAuthorizationService
{
    Task<bool> HasPermissionAsync(Guid userId, Guid groupId, string permissionCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetUserPermissionsInGroupAsync(Guid userId, Guid groupId, CancellationToken cancellationToken = default);
    Task InvalidateUserPermissionsCacheAsync(Guid userId, Guid groupId, CancellationToken cancellationToken = default);
}
