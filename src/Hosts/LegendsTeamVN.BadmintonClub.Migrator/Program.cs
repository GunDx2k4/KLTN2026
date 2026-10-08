using LegendsTeamVN.BadmintonClub.Migrator;
using LegendsTeamVN.BadmintonClub.Persistence.DependencyInjection.Extensions;
using LegendsTeamVN.Core.Identity.Data;
using LegendsTeamVN.Core.Identity.DependencyInjection.Extensions;
using LegendsTeamVN.Core.Identity.Entities;
using LegendsTeamVN.Core.Infrastructure.DependencyInjection.Extensions;
using LegendsTeamVN.Core.Utilities.Options;

var builder = Host.CreateApplicationBuilder(args);
builder.AddCoreSerilog();



builder.Services.Configure<ConnectionStringsOptions>(builder.Configuration.GetSection(ConnectionStringsOptions.SectionName));
var connectionStrings = new ConnectionStringsOptions();
builder.Configuration.GetSection(ConnectionStringsOptions.SectionName).Bind(connectionStrings);

// Migrations/seeders only need the publisher; they do not run API command handlers.
builder.Services.AddMediatR(options => options.RegisterServicesFromAssembly(typeof(Worker).Assembly));

builder.Services.AddIdentity<AppUser, AppRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 6;
        })
        .AddEntityFrameworkStores<AppIdentityDbContext>();

builder.Services.AddPostgreSQLIdentity(connectionStrings);

builder.Services.AddIdentityServices().AddIdentityContext();

builder.Services.AddRedisInfrastructure(builder.Configuration);

builder.Services.AddPersistence(connectionStrings).AddDataSeederBadminton();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
