using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Extensions;
using LegendsTeamVN.BadmintonClub.Infrastructure.DependencyInjection.Extensions;
using LegendsTeamVN.BadmintonClub.Persistence.DependencyInjection.Extensions;
using LegendsTeamVN.BadmintonClub.Presentation.DependencyInjection.Extensions;
using LegendsTeamVN.Core.Identity.DependencyInjection.Extensions;
using LegendsTeamVN.Core.Identity.DependencyInjection.Options;
using LegendsTeamVN.Core.Infrastructure.DependencyInjection.Extensions;
using LegendsTeamVN.Core.Presentation.DependencyInjection.Extensions;
using LegendsTeamVN.Core.Presentation.Extensions;
using LegendsTeamVN.Core.Utilities.Options;

var builder = WebApplication.CreateBuilder(args);
builder.AddCoreSerilog();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
var jwtOptions = new JwtOptions();
builder.Configuration.GetSection(JwtOptions.SectionName).Bind(jwtOptions);

builder.Services.Configure<ConnectionStringsOptions>(builder.Configuration.GetSection(ConnectionStringsOptions.SectionName));
var connectionStrings = new ConnectionStringsOptions();
builder.Configuration.GetSection(ConnectionStringsOptions.SectionName).Bind(connectionStrings);

// Add Application Infrastructure Services
builder.Services.AddInfrastructure(builder.Configuration);

#region Identity Services
builder.Services.AddCoreIdentity(connectionStrings, jwtOptions);
#endregion

builder.Services.AddApplication();

#region Persistence Services
builder.Services.AddPersistence(connectionStrings);
#endregion

#region Presentation Services
builder.Services.AddCorePresentation();
builder.Services.AddCoreSwagger("Badminton Club API");
#endregion

// Add Endpoints Discovery from Presentation assembly
builder.Services.AddPresentation();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseCoreSwagger();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Use Global Exception Handler
app.UseExceptionHandler();

// Map all Minimal API endpoints automatically
app.MapEndpoints();

// Seed initial data & sync permissions in AppPermissions & AppRolePermissions on startup
using (var scope = app.Services.CreateScope())
{
    var seeders = scope.ServiceProvider.GetServices<LegendsTeamVN.Core.Application.Data.IDataSeeder>();
    foreach (var seeder in seeders)
    {
        await seeder.SeedAsync();
    }
}

app.Run();
