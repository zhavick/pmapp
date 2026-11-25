using System.Runtime.Loader;
using Core.Api.Domain;
using Core.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PMApp.Plugin.Abstractions;

namespace Core.Api.Infrastructure.Plugins;

public sealed record LoadedPluginDescriptor(
    IPlugin Plugin,
    string DirectoryPath,
    bool HasFrontendBundle,
    IReadOnlyList<PluginPermissionDefinition> Permissions);

public sealed class PluginRegistry
{
    private readonly List<LoadedPluginDescriptor> _loaded = new();
    private readonly List<(string FilePath, string Error)> _failed = new();

    public IReadOnlyList<LoadedPluginDescriptor> LoadedPlugins => _loaded;
    public IReadOnlyList<(string FilePath, string Error)> FailedPlugins => _failed;

    internal void AddLoaded(LoadedPluginDescriptor descriptor) => _loaded.Add(descriptor);
    internal void AddFailed(string filePath, string error) => _failed.Add((filePath, error));
}

public static class SemVerHelper
{
    public static bool IsCompatible(string coreVersionStr, string rangeExpression)
    {
        if (!Version.TryParse(coreVersionStr, out var core)) return false;
        var parts = rangeExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.StartsWith(">=") && Version.TryParse(part[2..], out var min) && core < min) return false;
            if (part.StartsWith("<") && !part.StartsWith("<=") && Version.TryParse(part[1..], out var max) && core >= max) return false;
            if (part.StartsWith("<=") && Version.TryParse(part[2..], out var maxEq) && core > maxEq) return false;
        }
        return true;
    }
}

public static class PluginStartupLoader
{
    public const string CoreVersion = "1.0.0";

    public static PluginRegistry LoadPlugins(
        IServiceCollection services,
        IConfiguration configuration,
        string? adminConnStr,
        bool runMigrations)
    {
        var registry = new PluginRegistry();
        services.AddSingleton(registry);

        var pluginRoot = configuration["Plugins:Directory"];
        if (string.IsNullOrWhiteSpace(pluginRoot) || !Directory.Exists(pluginRoot))
        {
            return registry;
        }

        // Support both flat *.Plugin.dll or subdirectories plugins/{id}/*.dll
        var dllFiles = Directory.GetFiles(pluginRoot, "*Plugin.dll", SearchOption.AllDirectories);

        foreach (var dllPath in dllFiles)
        {
            try
            {
                var fullPath = Path.GetFullPath(dllPath);
                var dirPath = Path.GetDirectoryName(fullPath)!;
                var alc = new AssemblyLoadContext($"Plugin_{Path.GetFileNameWithoutExtension(fullPath)}", isCollectible: false);
                var asm = alc.LoadFromAssemblyPath(fullPath);

                var pluginTypes = asm.GetTypes()
                    .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                foreach (var type in pluginTypes)
                {
                    if (Activator.CreateInstance(type) is not IPlugin plugin) continue;

                    if (!SemVerHelper.IsCompatible(CoreVersion, plugin.Manifest.RequiresCore))
                    {
                        registry.AddFailed(fullPath, $"Incompatible core version. Requires {plugin.Manifest.RequiresCore}, Core is {CoreVersion}.");
                        continue;
                    }

                    if (runMigrations && !string.IsNullOrWhiteSpace(adminConnStr) && plugin is IPluginDbMigration migrator)
                    {
                        using var conn = new NpgsqlConnection(adminConnStr);
                        conn.Open();
                        migrator.ApplyMigrationsAsync(conn).GetAwaiter().GetResult();
                    }

                    plugin.ConfigureServices(services, configuration);

                    var permissions = plugin is IPluginPermissionProvider permProvider
                        ? permProvider.GetPermissions()
                        : Array.Empty<PluginPermissionDefinition>();

                    var hasFrontend = File.Exists(Path.Combine(dirPath, "frontend", "index.es.js"))
                                      || File.Exists(Path.Combine(dirPath, "index.es.js"));

                    registry.AddLoaded(new LoadedPluginDescriptor(plugin, dirPath, hasFrontend, permissions));
                }
            }
            catch (Exception ex)
            {
                // Isolate plugin failure so Core continues booting
                registry.AddFailed(dllPath, ex.Message);
            }
        }

        return registry;
    }

    public static async Task SyncCatalogAsync(AppDbContext db, PluginRegistry registry, CancellationToken ct = default)
    {
        foreach (var desc in registry.LoadedPlugins)
        {
            var m = desc.Plugin.Manifest;
            var existing = await db.PluginCatalog.FirstOrDefaultAsync(p => p.PluginId == m.PluginId, ct);
            if (existing is null)
            {
                db.PluginCatalog.Add(new PluginCatalogItem
                {
                    PluginId = m.PluginId,
                    Name = m.Name,
                    Description = m.Description,
                    Version = m.Version,
                    IsGloballyEnabled = true,
                    DefaultPrice = m.DefaultPrice
                });
            }
            else
            {
                existing.Name = m.Name;
                existing.Description = m.Description;
                existing.Version = m.Version;
                existing.DefaultPrice = m.DefaultPrice;
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
