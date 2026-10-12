namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Requests;

public sealed record ApproveJoinRequest(Guid? RoleId, string? MemberType);
