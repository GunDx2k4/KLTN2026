using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.UnitTests.Domain.Clubs;

[Trait("Category", "Domain")]
[Trait("FR", "FR03")]
public sealed class JoinRequestTests
{
    [Fact]
    public void JoinRequest_ApproveAndReject_ShouldUpdateStatusAndReviewer()
    {
        var clubId = Guid.NewGuid();
        var applicantId = Guid.NewGuid();
        var approverId = Guid.NewGuid();

        var request = new JoinRequest(clubId, applicantId, "Xin gia nhập nhóm");
        request.Status.Should().Be(JoinRequestStatus.PENDING);

        request.Approve(approverId);
        request.Status.Should().Be(JoinRequestStatus.APPROVED);
        request.ApprovedBy.Should().Be(approverId);
        request.ReviewedAt.Should().NotBeNull();

        var request2 = new JoinRequest(clubId, applicantId, "Xin gia nhập nhóm 2");
        request2.Reject(approverId);
        request2.Status.Should().Be(JoinRequestStatus.REJECTED);
        request2.ApprovedBy.Should().Be(approverId);
    }
}
