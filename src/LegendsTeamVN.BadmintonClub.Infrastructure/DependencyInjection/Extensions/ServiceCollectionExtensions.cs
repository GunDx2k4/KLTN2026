using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Infrastructure.Services;
using LegendsTeamVN.Core.Infrastructure.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using LegendsTeamVN.BadmintonClub.Infrastructure.Realtime;
using LegendsTeamVN.Core.Identity.DependencyInjection.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCoreInfrastructure(configuration);
        // Session operations authentication, realtime and outbox worker.
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        services.AddAuthentication().AddJwtBearer("SessionOperationsBearer", options =>
        {
            options.MapInboundClaims = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
                ValidateIssuerSigningKey = true, RequireExpirationTime = true, RequireSignedTokens = true,
                ValidIssuer = jwt.Issuer, ValidAudience = jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)), ClockSkew = TimeSpan.Zero
            };
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/hubs/sessions") &&
                        context.Request.Query.TryGetValue("access_token", out var token))
                        context.Token = token.ToString();
                    return Task.CompletedTask;
                }
            };
        });
        services.AddAuthorization(options => options.AddPolicy("SessionOperations", policy =>
            policy.AddAuthenticationSchemes("SessionOperationsBearer").RequireAuthenticatedUser()));
        var signalR = services.AddSignalR();
        var redis = configuration["ConnectionStrings:Redis"];
        if (!string.IsNullOrWhiteSpace(redis))
            signalR.AddStackExchangeRedis(redis, options => options.Configuration.ChannelPrefix =
                StackExchange.Redis.RedisChannel.Literal("BadmintonClub.SessionRotation"));
        services.AddSingleton<IUserIdProvider, SessionUserIdProvider>();
        services.AddScoped<ISessionRealtimePublisher, SignalRSessionRealtimePublisher>();
        services.AddHostedService<RotationOutboxWorker>();
        services.AddHostedService<RotationSchedulingWorker>();

        // Khởi tạo Firebase Admin SDK
        var credentialsJson = configuration["Firebase:CredentialsJson"];
        var credentialsPath = configuration["Firebase:CredentialsPath"] ?? "sports-venue-cb204-firebase-adminsdk-fbsvc-7f5ec12fb1.json";

        if (FirebaseApp.DefaultInstance == null)
        {
#pragma warning disable CS0618
            if (!string.IsNullOrWhiteSpace(credentialsJson))
            {
                FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.FromJson(credentialsJson)
                });
            }
            else
            {
                var fullPath = Path.IsPathRooted(credentialsPath)
                    ? credentialsPath
                    : Path.Combine(AppContext.BaseDirectory, credentialsPath);

                if (File.Exists(fullPath))
                {
                    FirebaseApp.Create(new AppOptions
                    {
                        Credential = GoogleCredential.FromFile(fullPath)
                    });
                }
            }
#pragma warning restore CS0618
        }
        // Đăng ký Service Notification
        services.AddScoped<INotificationService, FirebaseNotificationService>();
        services.AddScoped<IExternalAuthService, ExternalAuthService>();
        return services;
    }
}
