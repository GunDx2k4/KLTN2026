using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Explore;

public sealed record ExploreClubsQuery(
    double? Latitude,
    double? Longitude,
    double RadiusKm = 10,
    SkillLevel? SkillLevel = null,
    bool? IsRecruiting = null,
    string? SearchTerm = null
) : IQuery<List<ExploreClubResponse>>;
