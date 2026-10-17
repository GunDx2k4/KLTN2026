using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;

public sealed record UpdateSessionStatusRequest(SessionStatus Status, bool ConfirmRemainingNoShows = false);
