using Microsoft.AspNetCore.Authorization;

namespace LegendsTeamVN.BadmintonClub.Presentation.Security;

public sealed class GroupPermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
