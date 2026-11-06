using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Extensions;
using LegendsTeamVN.BadmintonClub.Migrator;
using LegendsTeamVN.BadmintonClub.Persistence.DependencyInjection.Extensions;
using LegendsTeamVN.Core.Identity.Data;
using LegendsTeamVN.Core.Identity.DependencyInjection.Extensions;
using LegendsTeamVN.Core.Identity.Entities;
using LegendsTeamVN.Core.Infrastructure.DependencyInjection.Extensions;
using LegendsTeamVN.Core.Utilities.Options;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Infrastructure.Services;
using LegendsTeamVN.BadmintonClub.Persistence;

var builder = Host.CreateApplicationBuilder(args);
builder.AddCoreSerilog();



builder.Services.Configure<ConnectionStringsOptions>(builder.Configuration.GetSection(ConnectionStringsOptions.SectionName));
var connectionStrings = new ConnectionStringsOptions();
builder.Configuration.GetSection(ConnectionStringsOptions.SectionName).Bind(connectionStrings);

builder.Services.AddApplication();
builder.Services.AddScoped<IExternalAuthService, ExternalAuthService>();

builder.Services.AddIdentity<AppUser, AppRole>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 3;
        })
        .AddEntityFrameworkStores<AppIdentityDbContext>();

builder.Services.AddPostgreSQLIdentity(connectionStrings).AddDataSeederIdentity();

builder.Services.AddIdentityServices().AddIdentityContext();

builder.Services.AddCoreInfrastructure(builder.Configuration);

builder.Services.AddPersistence(connectionStrings).AddDataSeederBadminton();

var verifyOnly = builder.Configuration.GetValue<bool>("Migrations:VerifyOnly");
if (!verifyOnly) builder.Services.AddHostedService<Worker>();

var host = builder.Build();
if (verifyOnly)
{
    using (host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        try
        {
            await DatabaseSchemaVerifier.VerifyAsync(scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>());
            await DatabaseSchemaVerifier.VerifyAsync(scope.ServiceProvider.GetRequiredService<BadmintonDbContext>());
            host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SchemaVerification")
                .LogInformation("Schema Identity và BadmintonClub đã sẵn sàng; không chạy migration hoặc seeder.");
            return 0;
        }
        catch (Exception exception)
        {
            host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SchemaVerification")
                .LogError("Kiểm tra schema thất bại: {Type}", exception.GetType().Name);
            return 1;
        }
    }
}
await host.RunAsync();
return Environment.ExitCode;



