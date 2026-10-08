using LegendsTeamVN.BadmintonClub.Application.Abstractions.Identity;
using LegendsTeamVN.Core.Identity.Abstractions;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Identity;

// Adapts the existing Identity implementation to the application's identity port.
public sealed class CurrentUserContext(ICurrentUserService currentUser) : ICurrentUserContext
{
    public Guid? UserId => currentUser.UserId;
    public bool IsAuthenticated => currentUser.IsAuthenticated;
}
