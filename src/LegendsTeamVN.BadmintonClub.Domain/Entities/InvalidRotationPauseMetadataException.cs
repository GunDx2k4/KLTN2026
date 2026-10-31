namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public sealed class InvalidRotationPauseMetadataException(Guid sessionId, Guid userId)
    : InvalidOperationException("Stored rotation pause metadata is invalid.")
{
    public Guid SessionId { get; } = sessionId;
    public Guid UserId { get; } = userId;
}
