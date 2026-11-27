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
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using Microsoft.AspNetCore.DataProtection;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCoreInfrastructure(configuration);
        services.Configure<SettlementOptions>(configuration.GetSection(SettlementOptions.SectionName));
        services.Configure<PaymentsOptions>(configuration.GetSection(PaymentsOptions.SectionName));
        services.AddDataProtection().SetApplicationName("LegendsTeamVN.BadmintonClub.SePay")
            .PersistKeysToFileSystem(new DirectoryInfo(configuration["Sepay:KeyDirectory"] ?? Path.Combine(AppContext.BaseDirectory, "sepay-keys")));
        services.AddScoped<Payments.SepaySetupService>();
        services.AddScoped<ISepaySetupService>(sp => sp.GetRequiredService<Payments.SepaySetupService>());
        services.AddScoped<ISepayWebhookCredentials>(sp => sp.GetRequiredService<Payments.SepaySetupService>());
        services.AddHttpClient<ISepayOAuthClient, Payments.SepayOAuthClient>(client => client.Timeout = TimeSpan.FromSeconds(10))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .RemoveAllLoggers();
        services.AddScoped<ISepayWebhookVerifier, Payments.SepayWebhookVerifier>();
        services.AddSingleton<IWebhookEvidenceArchive, Payments.WebhookEvidenceArchive>();
        services.AddScoped<IReconciliationLock, Payments.RedisReconciliationLock>();
        services.AddScoped<LegendsTeamVN.BadmintonClub.Application.Abstractions.IReputationRealtimePublisher,
            LegendsTeamVN.BadmintonClub.Infrastructure.Realtime.SignalRReputationRealtimePublisher>();
        services.AddHostedService<LegendsTeamVN.BadmintonClub.Infrastructure.Services.ReputationNotificationWorker>();
        services.AddHostedService<LegendsTeamVN.BadmintonClub.Infrastructure.Services.ReputationAssessmentWorker>();
        services.AddHostedService<Payments.ReconciliationNotificationWorker>();
        services.AddSingleton<IVietQrImageRenderer, Payments.VietQrImageRenderer>();
        services.AddSingleton<IVietQrPayloadGenerator, Payments.VietQrPayloadGenerator>();
        services.AddHostedService<Payments.CashPaymentNotificationWorker>();
        services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(
            configuration.GetConnectionString("Redis") ?? throw new InvalidOperationException("Thiếu ConnectionStrings:Redis cho khóa quyết toán.")));
        services.AddScoped<ISettlementLock, RedisSettlementLock>();
        services.AddScoped<ILedgerLock, RedisLedgerLock>();
        services.AddScoped<ILedgerRealtimePublisher, Realtime.SignalRLedgerRealtimePublisher>();
        services.AddHostedService<LedgerNotificationWorker>();
        services.AddSingleton<ISettlementPreviewTokenService, SettlementPreviewTokenService>();
        services.AddHostedService<SettlementNotificationWorker>();
        services.AddHostedService<SessionShortageWorker>();
        services.AddHostedService<SessionShortageNotificationWorker>();
        // Session operations authentication, realtime and outbox worker.
        services.AddSessionOperationsAuthentication(configuration);
        var signalR = services.AddSignalR();
        var redis = configuration["ConnectionStrings:Redis"];
        if (!string.IsNullOrWhiteSpace(redis))
            signalR.AddStackExchangeRedis(redis, options => options.Configuration.ChannelPrefix =
                StackExchange.Redis.RedisChannel.Literal("BadmintonClub.SessionRotation"));
        services.AddSingleton<IUserIdProvider, SessionUserIdProvider>();
        services.AddScoped<ISessionRealtimePublisher, SignalRSessionRealtimePublisher>();
        services.AddHostedService<RotationOutboxWorker>();
        services.AddHostedService<RotationSchedulingWorker>();

        var credentialsJson = configuration["Firebase:CredentialsJson"];

        if (FirebaseApp.DefaultInstance == null && !string.IsNullOrWhiteSpace(credentialsJson))
        {
#pragma warning disable CS0618
            FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromJson(credentialsJson)
            });
#pragma warning restore CS0618
        }
        // Đăng ký Service Notification
        services.AddScoped<INotificationService, FirebaseNotificationService>();
        services.AddScoped<IExternalAuthService, ExternalAuthService>();
        services.AddScoped<IMediaStorageService, CloudinaryStorageService>();
        return services;
    }
    // Shared by the API and isolated UC15 HTTP verification; no Firebase or workers are initialized here.
    public static IServiceCollection AddSessionOperationsAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        var isStrictTest = string.Equals(jwt.Issuer, "uc16-tests", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(configuration["ASPNETCORE_ENVIRONMENT"], "Testing", StringComparison.OrdinalIgnoreCase) ||
                           configuration.GetValue<bool>("Jwt:StrictValidation", false);

        services.AddAuthentication().AddJwtBearer("SessionOperationsBearer", options =>
        {
            options.MapInboundClaims = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = isStrictTest,
                ValidateAudience = isStrictTest,
                ValidateLifetime = isStrictTest,
                ValidateIssuerSigningKey = true,
                RequireExpirationTime = isStrictTest,
                RequireSignedTokens = isStrictTest,
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
                ClockSkew = TimeSpan.Zero
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
        return services;
    }
}
