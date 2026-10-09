namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Requests;

public sealed record CreateClubRequest(string Name, string? Description, string? AvatarUrl);
