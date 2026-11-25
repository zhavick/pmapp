using System.Text;
using Core.Api.Common;
using Core.Api.Infrastructure.Auditing;
using Core.Api.Infrastructure.Auth;
using Core.Api.Infrastructure.Persistence;
using Core.Api.Infrastructure.Plugins;
using Core.Api.Infrastructure.Tenancy;
using Core.Api.Modules.Auth;
using Core.Api.Modules.Clients;
using Core.Api.Modules.PlatformAdmin;
using Core.Api.Modules.Plugins;
using Core.Api.Modules.Projects;
using Core.Api.Modules.Rbac;
using Core.Api.Modules.Team;
using Core.Api.Modules.Workspace;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PMApp.Plugin.Abstractions;

var builder = WebApplication.CreateBuilder(args);

var adminConnStr = builder.Configuration.GetConnectionString("Admin")
    ?? "Host=localhost;Port=5432;Database=pmapp_dev;Username=postgres;Password=pmapp_dev_password";
var appConnStr = builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5432;Database=pmapp_dev;Username=pmapp_app;Password=pmapp_app_password";

// Ensure Core schema, non-superuser role, and RLS policies exist before loading plugins
if (!builder.Environment.IsEnvironment("Testing"))
{
    await DatabaseInitializer.InitializeAsync(adminConnStr);
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddProblemDetails();
builder.Services.AddSignalR();
builder.Services.AddHostedService<PlatformMaintenanceWorker>();

builder.Services.AddScoped<HttpTenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<HttpTenantContext>());
builder.Services.AddScoped<IPluginEntitlementChecker, DbPluginEntitlementChecker>();
builder.Services.AddScoped<TenantRlsConnectionInterceptor>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddSingleton<JwtTokenService>();

builder.Services.AddDbContext<AppDbContext>((sp, opt) =>
{
    opt.UseNpgsql(appConnStr)
       .AddInterceptors(
           sp.GetRequiredService<TenantRlsConnectionInterceptor>(),
           sp.GetRequiredService<AuditSaveChangesInterceptor>());
});

// Load compiled plugins at startup (Decision D5)
var pluginRegistry = PluginStartupLoader.LoadPlugins(
    builder.Services,
    builder.Configuration,
    adminConnStr,
    runMigrations: true);

var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "dev-only-super-secret-signing-key-min-32-chars-pmapp";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "pmapp";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "pmapp-web";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdminOnly", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("is_super_admin", "true"));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

var app = builder.Build();

// Sync discovered plugins to PluginCatalog table
if (pluginRegistry.LoadedPlugins.Count > 0)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await PluginStartupLoader.SyncCatalogAsync(db, pluginRegistry);
}

app.UseExceptionHandler();
app.UseCors("FrontendDev");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/v1/health", () => Results.Ok(new
{
    status = "healthy",
    coreVersion = PluginStartupLoader.CoreVersion,
    loadedPlugins = pluginRegistry.LoadedPlugins.Select(p => p.Plugin.Manifest.PluginId),
    failedPlugins = pluginRegistry.FailedPlugins.Select(f => new { f.FilePath, f.Error })
}));

app.MapAuthEndpoints();
app.MapClientEndpoints();
app.MapTeamEndpoints();
app.MapProjectEndpoints();
app.MapWorkspaceEndpoints();
app.MapRbacEndpoints();
app.MapExtensionsEndpoints();
app.MapTenantPluginsEndpoints();
app.MapPlatformAdminEndpoints();
app.MapHub<WorkspaceHub>("/hubs/workspace");

// Map each loaded plugin under /api/v1/p/{pluginId} protected by Entitlement Gate
foreach (var desc in pluginRegistry.LoadedPlugins)
{
    var pluginId = desc.Plugin.Manifest.PluginId;
    var group = app.MapGroup($"/api/v1/p/{pluginId}")
        .RequireAuthorization()
        .AddEndpointFilter(new PluginEntitlementEndpointFilter(pluginId));
    desc.Plugin.MapEndpoints(group);
}

app.Run();

public partial class Program { }
