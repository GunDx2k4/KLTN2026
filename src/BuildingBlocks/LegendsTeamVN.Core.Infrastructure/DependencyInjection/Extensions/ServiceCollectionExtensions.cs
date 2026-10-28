using System.Reflection;

using LegendsTeamVN.Core.Application.Caching;
using LegendsTeamVN.Core.Application.Data;
using LegendsTeamVN.Core.Application.Messaging.Events;
using LegendsTeamVN.Core.Infrastructure.Caching;
using LegendsTeamVN.Core.Infrastructure.Data;
using LegendsTeamVN.Core.Infrastructure.DependencyInjection.Options;
using LegendsTeamVN.Core.Infrastructure.Messaging;

using MassTransit;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.Core.Infrastructure.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var databaseConnectionString = configuration.GetConnectionString("Database");
        if (!string.IsNullOrEmpty(databaseConnectionString))
        {
            services.AddTransient<ISqlConnectionFactory>(_ => 
                new SqlConnectionFactory(databaseConnectionString));
        }

        services.AddRedisInfrastructure(configuration);

        //services.AddMasstransitRabbitMQInfrastructure(configuration);
        return services;
    }

    public static IServiceCollection AddRedisInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
            });
            services.AddSingleton<ICacheService, RedisCacheService>();
        }

        return services;
    }

    public static IServiceCollection AddMasstransitRabbitMQInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var masstransitSection = configuration.GetSection("MasstransitConfiguration");
        if (!masstransitSection.Exists())
        {
            return services;
        }
        services.Configure<MasstransitConfiguration>(masstransitSection);

        services.AddMassTransit(cfg =>
        {
            cfg.AddConsumers(Assembly.GetExecutingAssembly());

            cfg.SetKebabCaseEndpointNameFormatter();

            cfg.UsingRabbitMq((context, rabbitConfig) =>
            {
                var masstransitConfig = context.GetRequiredService<IOptions<MasstransitConfiguration>>().Value;

                rabbitConfig.Host(masstransitConfig.Host, masstransitConfig.VHost, h =>
                {
                    h.Username(masstransitConfig.UserName);
                    h.Password(masstransitConfig.Password);
                });

            });
        });


        services.AddTransient<IEventBus, EventBus>();


        return services;
    }


}
