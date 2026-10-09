using LegendsTeamVN.Core.Application.Data;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Identity.Authorization;
using LegendsTeamVN.Core.Identity.Data;
using LegendsTeamVN.Core.Identity.DependencyInjection.Options;
using LegendsTeamVN.Core.Identity.Entities;
using LegendsTeamVN.Core.Identity.Services;
using LegendsTeamVN.Core.Utilities.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace LegendsTeamVN.Core.Identity.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreIdentity(this IServiceCollection services, ConnectionStringsOptions connectionStrings, JwtOptions jwtOptions)
    {
        services.AddPostgreSQLIdentity(connectionStrings);

        services.AddIdentity<AppUser, AppRole>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 3;
        })
        .AddEntityFrameworkStores<AppIdentityDbContext>()
        .AddDefaultTokenProviders();

        services.AddJwtAuthenticationAPI(jwtOptions);

        services.AddIdentityContext();
        services.AddIdentityServices();

        return services;
    }

    public static IServiceCollection AddPostgreSQLIdentity(this IServiceCollection services, ConnectionStringsOptions configureOptions)
    {
        services.AddDbContext<AppIdentityDbContext>(options =>
        {
            options.UseNpgsql(configureOptions.Database, b => b.MigrationsAssembly("LegendsTeamVN.BadmintonClub.Migrator"));
        });

        return services;
    }

    public static IServiceCollection AddJwtAuthenticationAPI(this IServiceCollection services, JwtOptions jwtOptions)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.SaveToken = true;
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false, // Disabled Lifetime Validation for Dev
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                ClockSkew = TimeSpan.Zero
            };
        });

        services.AddAuthorization();

        return services;
    }

    public static IServiceCollection AddIdentityServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IUserManagerService, UserManagerService>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IDataSeeder, IdentityDataSeeder>();

        return services;
    }

    public static IServiceCollection AddIdentityContext(this IServiceCollection services)
    {
        services.AddScoped<AppIdentityDbContext>();
        return services;
    }

    public static IServiceCollection AddDataSeederIdentity(this IServiceCollection services)
    {
        services.AddScoped<IDataSeeder, IdentityDataSeeder>();
        return services;
    }
}

