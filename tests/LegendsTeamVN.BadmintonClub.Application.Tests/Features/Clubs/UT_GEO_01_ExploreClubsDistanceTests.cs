using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Requests;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Explore;
using LegendsTeamVN.BadmintonClub.Application.Tests.Common;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Clubs;

public class UT_GEO_01_ExploreClubsDistanceTests
{
    [Fact]
    public async Task Handle_WithGpsCoordinates_ShouldFilterClubsWithin5KmRadiusAccurately()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var clubRepo = new ClubRepository(dbContext);
        var handler = new ExploreClubsQueryHandler(clubRepo);

        // Group A: Tọa độ (21.0350, 105.8500) - Cách người chơi ~0.84 km
        var groupA = new ClubGroup(
            groupName: "CLB Cầu Lông Ba Đình (Group A)",
            lat: 21.0350m,
            lng: 105.8500m,
            isActive: true,
            code: "GRP_A"
        );

        // Group B: Tọa độ rất xa (20.5000, 105.0000) - Cách người chơi > 50 km
        var groupB = new ClubGroup(
            groupName: "CLB Ngoại Thành (Group B)",
            lat: 20.5000m,
            lng: 105.0000m,
            isActive: true,
            code: "GRP_B"
        );

        dbContext.ClubGroups.AddRange(groupA, groupB);
        await dbContext.SaveChangesAsync();

        // Người chơi tại tọa độ GPS: (21.0285, 105.8542), Bán kính quét = 5.0 km
        var query = new ExploreClubsQuery(
            SearchTerm: null,
            Latitude: 21.0285,
            Longitude: 105.8542,
            RadiusKm: 5.0,
            SkillLevel: null,
            IsRecruiting: null
        );

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        // Group A trong bán kính 5km phải được trả về
        result.Value.Should().ContainSingle(c => c.Id == groupA.Id);
        var returnedGroupA = result.Value.First(c => c.Id == groupA.Id);
        returnedGroupA.DistanceKm.Should().NotBeNull();
        returnedGroupA.DistanceKm!.Value.Should().BeInRange(0.5, 1.2); // ~0.84km

        // Group B ngoài bán kính 5km phải bị lọc bỏ
        result.Value.Should().NotContain(c => c.Id == groupB.Id);
    }
}
