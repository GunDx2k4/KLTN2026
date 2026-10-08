using LegendsTeamVN.Core.Infrastructure.DependencyInjection.Extensions;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Application.Abstractions.Identity;
using LegendsTeamVN.BadmintonClub.Infrastructure.Identity;
using LegendsTeamVN.BadmintonClub.Infrastructure.Locking;
using LegendsTeamVN.BadmintonClub.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCoreInfrastructure(configuration);
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(configuration.GetConnectionString("Redis")
                ?? throw new InvalidOperationException("ConnectionStrings:Redis is required for RSVP."));
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddSingleton<IMatchRsvpLock, RedisMatchRsvpLock>();
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();
        return services;
    }

    public static IServiceCollection AddMatchRealtime<THub>(this IServiceCollection services,
        IConfiguration configuration, Func<Guid, string> groupName) where THub : Hub
    {
        var redis = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("ConnectionStrings:Redis is required for match realtime updates.");
        services.AddSignalR()
            .AddStackExchangeRedis(redis, options =>
            {
                options.Configuration.AbortOnConnectFail = false;
                options.Configuration.ChannelPrefix = RedisChannel.Literal("LegendsTeamVN.BadmintonClub.SignalR");
            });
        services.AddScoped<IMatchAttendanceNotifier>(provider =>
            new SignalRMatchAttendanceNotifier<THub>(provider.GetRequiredService<IHubContext<THub>>(), groupName));
        return services;
    }
}
