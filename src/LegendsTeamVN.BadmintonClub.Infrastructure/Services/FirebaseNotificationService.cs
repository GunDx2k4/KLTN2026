using FirebaseAdmin.Messaging;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Services;

public class FirebaseNotificationService : INotificationService
{
    private readonly ILogger<FirebaseNotificationService> _logger;

    public FirebaseNotificationService(ILogger<FirebaseNotificationService> logger)
    {
        _logger = logger;
    }

    public async Task<string> SendNotificationAsync(
        string deviceToken, 
        string title, 
        string body, 
        Dictionary<string, string>? data = null, 
        CancellationToken cancellationToken = default)
    {
        var message = new Message
        {
            Token = deviceToken,
            Notification = new Notification
            {
                Title = title,
                Body = body
            },
            Data = data
        };

        try
        {
            var response = await FirebaseMessaging.DefaultInstance.SendAsync(message, cancellationToken);
            _logger.LogInformation("Successfully sent message: {MessageId}", response);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification to token: {Token}", deviceToken);
            throw;
        }
    }

    public async Task<int> SendMulticastNotificationAsync(
        IReadOnlyList<string> deviceTokens, 
        string title, 
        string body, 
        Dictionary<string, string>? data = null, 
        CancellationToken cancellationToken = default)
    {
        if (deviceTokens.Count == 0) return 0;

        var message = new MulticastMessage
        {
            Tokens = deviceTokens,
            Notification = new Notification
            {
                Title = title,
                Body = body
            },
            Data = data
        };

        var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message, cancellationToken);
        _logger.LogInformation("Multicast sent: {SuccessCount} success, {FailureCount} failure", 
            response.SuccessCount, response.FailureCount);
        return response.SuccessCount;
    }

    public async Task<string> SendTopicNotificationAsync(
        string topic, 
        string title, 
        string body, 
        Dictionary<string, string>? data = null, 
        CancellationToken cancellationToken = default)
    {
        var message = new Message
        {
            Topic = topic,
            Notification = new Notification
            {
                Title = title,
                Body = body
            },
            Data = data
        };

        return await FirebaseMessaging.DefaultInstance.SendAsync(message, cancellationToken);
    }
}
