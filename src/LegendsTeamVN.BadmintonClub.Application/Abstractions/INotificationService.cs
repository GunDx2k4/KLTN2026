namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;

public interface INotificationService
{
    Task<string> SendNotificationAsync(
        string deviceToken, 
        string title, 
        string body, 
        Dictionary<string, string>? data = null, 
        CancellationToken cancellationToken = default);

    Task<int> SendMulticastNotificationAsync(
        IReadOnlyList<string> deviceTokens, 
        string title, 
        string body, 
        Dictionary<string, string>? data = null, 
        CancellationToken cancellationToken = default);

    Task<string> SendTopicNotificationAsync(
        string topic, 
        string title, 
        string body, 
        Dictionary<string, string>? data = null, 
        CancellationToken cancellationToken = default);
}
