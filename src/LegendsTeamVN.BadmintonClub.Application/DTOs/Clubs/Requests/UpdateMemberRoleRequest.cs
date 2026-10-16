namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Requests;

public record UpdateMemberRoleRequest(Guid? RoleId = null, int? Role = null, string? RoleName = null);
