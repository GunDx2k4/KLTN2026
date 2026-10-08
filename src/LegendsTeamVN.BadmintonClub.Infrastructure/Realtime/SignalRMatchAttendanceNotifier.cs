using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Matches.Realtime;
using LegendsTeamVN.BadmintonClub.Domain.Models;
using Microsoft.AspNetCore.SignalR;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Realtime;

// The API host supplies the concrete hub and group naming; Infrastructure never references Presentation.
public sealed class SignalRMatchAttendanceNotifier<THub>(IHubContext<THub> hub, Func<Guid, string> groupName)
    : IMatchAttendanceNotifier where THub : Hub
{
    public async Task NotifyAsync(MatchAttendanceSnapshot match, Guid userId, CancellationToken cancellationToken)
    {
        await hub.Clients.Group(groupName(match.MatchId)).SendAsync("MatchAttendanceChanged",
            new MatchAttendanceChanged(match.MatchId, match.ConfirmedCount, match.AvailableSlots, match.WaitlistCount, match.Status, match.AttendanceVersion),
            cancellationToken);
        await hub.Clients.User(userId.ToString()).SendAsync("MyRsvpChanged",
            new MyRsvpChanged(match.MatchId, match.PlayerStatus, match.WaitlistPosition, match.AttendanceVersion), cancellationToken);
    }
}
