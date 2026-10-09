using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Extensions;
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

builder.Services.AddApplication();

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

builder.Services.AddRedisInfrastructure(builder.Configuration);

builder.Services.AddPersistence(connectionStrings).AddDataSeederBadminton();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
