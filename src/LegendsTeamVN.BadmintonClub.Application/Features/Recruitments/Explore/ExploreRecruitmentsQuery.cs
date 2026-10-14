using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Recruitments.Explore;

public sealed record ExploreRecruitmentsQuery(
    double? Latitude = null,
    double? Longitude = null,
    double RadiusKm = 10,
    SkillLevel? MinSkillLevel = null
) : IQuery<List<GuestRecruitmentResponse>>;
