using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Infrastructure.Services;
using LegendsTeamVN.Core.Infrastructure.DependencyInjection.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCoreInfrastructure(configuration);
         // Khởi tạo Firebase Admin SDK
        var credentialsPath = configuration["Firebase:CredentialsPath"] ?? "sports-venue-cb204-firebase-adminsdk-fbsvc-7f5ec12fb1.json";
        var fullPath = Path.Combine(AppContext.BaseDirectory, credentialsPath);
        if (File.Exists(fullPath) && FirebaseApp.DefaultInstance == null)
        {
            FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromFile(fullPath)
            });
        }
        // Đăng ký Service Notification
        services.AddScoped<INotificationService, FirebaseNotificationService>();
        return services;
    }
}
