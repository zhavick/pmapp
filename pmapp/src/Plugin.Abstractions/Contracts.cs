using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace PMApp.Plugin.Abstractions;

/// <summary>
/// Marks a database entity as scoped to a specific tenant.
/// </summary>
public interface ITenantEntity
{
    Guid TenantId { get; set; }
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>
/// Marks an entity for automatic audit logging (Create/Update/Delete) via EF Core interceptor.
/// </summary>
public interface IAuditableEntity
{
    Guid Id { get; }
}

public interface ITenantContext
{
    Guid CurrentTenantId { get; }
    Guid? CurrentUserId { get; }
    bool IsSuperAdmin { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
}

public interface IPluginEntitlementChecker
{
    Task<bool> IsPluginEnabledForTenantAsync(Guid tenantId, string pluginId, CancellationToken ct = default);
}

public sealed record PluginManifest(
    string PluginId,
    string Name,
    string Version,
    string Description,
    string RequiresCore = ">=1.0.0 <2.0.0",
    string[]? Dependencies = null,
    decimal DefaultPrice = 0m);

public sealed record PluginPermissionDefinition(
    string Key,
    string DisplayName,
    string Category);

public interface IPlugin
{
    PluginManifest Manifest { get; }
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
    void MapEndpoints(IEndpointRouteBuilder pluginGroup);
}

public interface IPluginDbMigration
{
    Task ApplyMigrationsAsync(NpgsqlConnection adminConnection, CancellationToken ct = default);
}

public interface IPluginPermissionProvider
{
    IReadOnlyList<PluginPermissionDefinition> GetPermissions();
}
