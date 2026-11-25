using System.Data.Common;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Spike.Abstractions;

var builder = WebApplication.CreateBuilder(args);

var connStr = builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5439;Database=pmapp_spike;Username=pmapp_app;Password=app_pass";

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HeaderTenantContext>();
builder.Services.AddScoped<IPluginEntitlementChecker, DbPluginEntitlementChecker>();
builder.Services.AddScoped<TenantRlsConnectionInterceptor>();

builder.Services.AddDbContext<SpikeDbContext>((sp, opt) =>
{
    opt.UseNpgsql(connStr)
       .AddInterceptors(sp.GetRequiredService<TenantRlsConnectionInterceptor>());
});

// Load plugins from folder if configured
var pluginDir = builder.Configuration["PluginDir"];
var loadedPlugins = new List<IPlugin>();
if (!string.IsNullOrWhiteSpace(pluginDir) && Directory.Exists(pluginDir))
{
    foreach (var dll in Directory.GetFiles(pluginDir, "*Plugin.dll"))
    {
        var alc = new AssemblyLoadContext(Path.GetFileNameWithoutExtension(dll), isCollectible: false);
        var asm = alc.LoadFromAssemblyPath(Path.GetFullPath(dll));
        foreach (var type in asm.GetTypes().Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract))
        {
            if (Activator.CreateInstance(type) is IPlugin plugin)
            {
                plugin.ConfigureServices(builder.Services);
                loadedPlugins.Add(plugin);
            }
        }
    }
}

var app = builder.Build();

// Core endpoint: EF Core query (filtered by Global Query Filter + RLS)
app.MapGet("/api/v1/projects", async (SpikeDbContext db) =>
{
    var items = await db.Projects.Select(p => new { p.Id, p.TenantId, p.Title }).ToListAsync();
    return Results.Ok(items);
});

// Core endpoint: Raw SQL WITHOUT WHERE tenant_id (tests PostgreSQL RLS backstop!)
app.MapGet("/api/v1/projects/raw", async (SpikeDbContext db) =>
{
    var items = await db.Database
        .SqlQueryRaw<ProjectRow>("SELECT id, tenant_id, title FROM projects")
        .ToListAsync();
    return Results.Ok(items);
});

// Map loaded plugins under /api/v1/p/{pluginId} with Entitlement Filter
foreach (var plugin in loadedPlugins)
{
    var group = app.MapGroup($"/api/v1/p/{plugin.PluginId}")
        .AddEndpointFilter(new PluginEntitlementEndpointFilter(plugin.PluginId));
    plugin.MapEndpoints(group);
}

app.Run();

public partial class Program { }

public sealed class HeaderTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    public Guid CurrentTenantId =>
        Guid.TryParse(accessor.HttpContext?.Request.Headers["X-Tenant-Id"].FirstOrDefault(), out var id)
            ? id
            : Guid.Empty;
}

public sealed class TenantRlsConnectionInterceptor(ITenantContext tenantContext) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT set_config('app.current_tenant_id', '{tenantContext.CurrentTenantId}', false);";
        cmd.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken ct = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT set_config('app.current_tenant_id', '{tenantContext.CurrentTenantId}', false);";
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

public sealed class PluginEntitlementEndpointFilter(string pluginId) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var tenantCtx = ctx.HttpContext.RequestServices.GetRequiredService<ITenantContext>();
        var checker = ctx.HttpContext.RequestServices.GetRequiredService<IPluginEntitlementChecker>();
        if (!await checker.IsPluginEnabledForTenantAsync(tenantCtx.CurrentTenantId, pluginId, ctx.HttpContext.RequestAborted))
        {
            return Results.Problem(
                title: "Plugin Not Active",
                detail: $"Plugin '{pluginId}' is not active for tenant '{tenantCtx.CurrentTenantId}'.",
                statusCode: StatusCodes.Status403Forbidden);
        }
        return await next(ctx);
    }
}

public sealed class DbPluginEntitlementChecker(SpikeDbContext db) : IPluginEntitlementChecker
{
    public async Task<bool> IsPluginEnabledForTenantAsync(Guid tenantId, string pluginId, CancellationToken ct = default)
    {
        return await db.Entitlements
            .IgnoreQueryFilters()
            .AnyAsync(e => e.TenantId == tenantId && e.PluginId == pluginId && e.Status == "Active", ct);
    }
}

public sealed class SpikeDbContext(DbContextOptions<SpikeDbContext> options, ITenantContext tenantContext) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Entitlement> Entitlements => Set<Entitlement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(b =>
        {
            b.ToTable("projects");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.TenantId).HasColumnName("tenant_id");
            b.Property(x => x.Title).HasColumnName("title");
            b.HasQueryFilter(x => x.TenantId == tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<Entitlement>(b =>
        {
            b.ToTable("tenant_plugin_entitlements");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.TenantId).HasColumnName("tenant_id");
            b.Property(x => x.PluginId).HasColumnName("plugin_id");
            b.Property(x => x.Status).HasColumnName("status");
        });
    }
}

public sealed class Project
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Title { get; set; } = "";
}

public sealed class Entitlement
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string PluginId { get; set; } = "";
    public string Status { get; set; } = "";
}

public sealed class ProjectRow
{
    public Guid id { get; set; }
    public Guid tenant_id { get; set; }
    public string title { get; set; } = "";
}
