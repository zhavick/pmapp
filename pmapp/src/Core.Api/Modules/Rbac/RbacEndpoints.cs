using Core.Api.Infrastructure.Persistence;
using Core.Api.Infrastructure.Plugins;
using Microsoft.EntityFrameworkCore;
using PMApp.Plugin.Abstractions;

namespace Core.Api.Modules.Rbac;

public static class RbacEndpoints
{
    private static readonly PluginPermissionDefinition[] CorePermissions =
    [
        new("projects.view", "View Projects", "Projects"),
        new("projects.manage", "Create & Edit Projects", "Projects"),
        new("tasks.manage", "Manage Tasks", "Tasks"),
        new("clients.view", "View Clients", "Clients"),
        new("clients.manage", "Manage Clients", "Clients"),
        new("team.manage", "Manage Team & Roles", "Team"),
        new("plugins.manage", "Manage Tenant Plugins", "Plugins"),
        new("settings.manage", "Manage Tenant Settings", "Settings")
    ];

    public static void MapRbacEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/rbac").RequireAuthorization();

        group.MapGet("/permissions", async (
            AppDbContext db,
            ITenantContext tenantContext,
            PluginRegistry registry) =>
        {
            var now = DateTimeOffset.UtcNow;
            var activePluginIds = (await db.TenantPluginEntitlements
                .IgnoreQueryFilters()
                .Where(e =>
                    e.TenantId == tenantContext.CurrentTenantId &&
                    e.Status == "Active" &&
                    e.IsEnabledByTenant &&
                    !e.IsDeleted &&
                    (e.ExpiresAt == null || e.ExpiresAt > now))
                .Select(e => e.PluginId)
                .ToListAsync()).ToHashSet();

            var result = CorePermissions
                .Select(p => new { p.Key, p.DisplayName, p.Category, PluginId = (string?)null })
                .ToList();

            foreach (var desc in registry.LoadedPlugins)
            {
                var pluginId = desc.Plugin.Manifest.PluginId;
                if (!activePluginIds.Contains(pluginId)) continue;

                foreach (var perm in desc.Permissions)
                {
                    result.Add(new { perm.Key, perm.DisplayName, perm.Category, PluginId = (string?)pluginId });
                }
            }

            return Results.Ok(result);
        });

        group.MapGet("/roles", async (AppDbContext db) =>
        {
            var roles = await db.Roles
                .Select(r => new { r.Id, r.Name, r.IsSystem })
                .ToListAsync();
            return Results.Ok(roles);
        });
    }
}
