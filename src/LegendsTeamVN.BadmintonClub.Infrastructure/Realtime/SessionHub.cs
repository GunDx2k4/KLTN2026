using System.Security.Claims;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Realtime;

[Authorize(Policy = "SessionOperations")]
public sealed class SessionHub(ISessionRepository sessions) : Hub
{
    public static string SessionGroup(Guid id) => $"Session_{id}";

    public async Task JoinSession(Guid sessionId)
    {
        if (!Guid.TryParse(Context.UserIdentifier, out var userId)) throw new HubException("Unauthenticated.");
        var session = await sessions.FindSingleAsync(s => s.Id == sessionId && !s.IsDeleted, Context.ConnectionAborted);
        if (session is null || !await sessions.CanManageRotationAsync(session.GroupId, userId, Context.ConnectionAborted))
            throw new HubException("Forbidden.");
        await Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(sessionId), Context.ConnectionAborted);
    }

    public Task LeaveSession(Guid sessionId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, SessionGroup(sessionId));
}

public sealed class SessionUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        var id = connection.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? connection.User?.FindFirstValue("sub");
        return Guid.TryParse(id, out var userId) ? userId.ToString() : null;
    }
}
