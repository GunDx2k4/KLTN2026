using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;

public interface ISettlementPreviewTokenService
{
    SettlementPreviewToken Create(Guid actorId, Guid groupId, Guid sessionId, SettlementCosts costs,
        IReadOnlyCollection<SettlementAdjustment> adjustments, bool confirmRemainingNoShows,
        string sourceHash, SettlementAllocation allocation, DateTime utcNow);
    Result<SettlementPreviewIdentity> Validate(string token, Guid actorId, Guid groupId, Guid sessionId,
        SettlementCosts costs, IReadOnlyCollection<SettlementAdjustment> adjustments, bool confirmRemainingNoShows);
    string HashSource(BadmintonSession session, GroupFeeRule rule, IReadOnlyCollection<AppUser> users,
        IReadOnlyCollection<GroupMember> memberships);
    string HashResult(SettlementAllocation allocation);
}

public sealed record SettlementPreviewToken(string Token, SettlementPreviewIdentity Identity);
public sealed record SettlementPreviewIdentity(int Version, string PolicyVersion, Guid PreviewId, Guid ActorId,
    Guid GroupId, Guid SessionId, DateTime IssuedAt, DateTime ExpiresAt, string RequestHash, string SourceHash, string ResultHash);
