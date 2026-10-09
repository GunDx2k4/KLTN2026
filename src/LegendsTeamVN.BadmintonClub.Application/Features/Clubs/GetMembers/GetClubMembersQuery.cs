using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.GetMembers;

public record GetClubMembersQuery(Guid ClubId) : IQuery<List<ClubMemberResponse>>;
