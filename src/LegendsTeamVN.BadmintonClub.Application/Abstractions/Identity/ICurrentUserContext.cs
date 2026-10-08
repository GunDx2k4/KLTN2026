namespace LegendsTeamVN.BadmintonClub.Application.Abstractions.Identity;

public interface ICurrentUserContext
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
}
