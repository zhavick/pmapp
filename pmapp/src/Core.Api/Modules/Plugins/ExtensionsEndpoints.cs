using Core.Api.Infrastructure.Persistence;
using Core.Api.Infrastructure.Plugins;
using Microsoft.EntityFrameworkCore;
using PMApp.Plugin.Abstractions;

namespace Core.Api.Modules.Plugins;

public static class ExtensionsEndpoints
{
    public static void MapExtensionsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/extensions/active", async (
            AppDbContext db,
            ITenantContext tenantContext,
            PluginRegistry registry) =>
        {
            var now = DateTimeOffset.UtcNow;
            var activeIds = (await db.TenantPluginEntitlements
                .IgnoreQueryFilters()
                .Where(e =>
                    e.TenantId == tenantContext.CurrentTenantId &&
                    e.Status == "Active" &&
                    e.IsEnabledByTenant &&
                    !e.IsDeleted &&
                    (e.ExpiresAt == null || e.ExpiresAt > now))
                .Select(e => e.PluginId)
                .ToListAsync()).ToHashSet();

            var extensions = registry.LoadedPlugins
                .Where(d => activeIds.Contains(d.Plugin.Manifest.PluginId) && d.HasFrontendBundle)
                .Select(d => new
                {
                    pluginId = d.Plugin.Manifest.PluginId,
                    name = d.Plugin.Manifest.Name,
                    version = d.Plugin.Manifest.Version,
                    bundleUrl = $"/plugins/{d.Plugin.Manifest.PluginId}/index.es.js"
                })
                .ToList();

            return Results.Ok(extensions);
        }).RequireAuthorization();

        // Serve static frontend assets for loaded plugins safely
        app.MapGet("/plugins/{pluginId}/{*filePath}", (string pluginId, string filePath, PluginRegistry registry) =>
        {
            var desc = registry.LoadedPlugins.FirstOrDefault(d => d.Plugin.Manifest.PluginId == pluginId);
            if (desc is null) return Results.NotFound();

            var frontendRoot = Directory.Exists(Path.Combine(desc.DirectoryPath, "frontend"))
                ? Path.Combine(desc.DirectoryPath, "frontend")
                : desc.DirectoryPath;

            var fullRoot = Path.GetFullPath(frontendRoot);
            var targetPath = Path.GetFullPath(Path.Combine(fullRoot, filePath));

            if (!targetPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(targetPath))
            {
                return Results.NotFound();
            }

            var contentType = Path.GetExtension(targetPath).ToLowerInvariant() switch
            {
                ".js" or ".mjs" => "application/javascript",
                ".css" => "text/css",
                ".json" => "application/json",
                ".svg" => "image/svg+xml",
                ".png" => "image/png",
                _ => "application/octet-stream"
            };

            return Results.File(targetPath, contentType);
        });
    }
}
