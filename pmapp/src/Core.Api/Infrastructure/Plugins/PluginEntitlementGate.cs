using Core.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using PMApp.Plugin.Abstractions;

namespace Core.Api.Infrastructure.Plugins;

public sealed class DbPluginEntitlementChecker(AppDbContext db) : IPluginEntitlementChecker
{
    public async Task<bool> IsPluginEnabledForTenantAsync(Guid tenantId, string pluginId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) return false;

        var globallyEnabled = await db.PluginCatalog
            .AnyAsync(p => p.PluginId == pluginId && p.IsGloballyEnabled, ct);

        if (!globallyEnabled) return false;

        var now = DateTimeOffset.UtcNow;
        return await db.TenantPluginEntitlements
            .IgnoreQueryFilters()
            .AnyAsync(e =>
                e.TenantId == tenantId &&
                e.PluginId == pluginId &&
                e.Status == "Active" &&
                e.IsEnabledByTenant &&
                !e.IsDeleted &&
                (e.ExpiresAt == null || e.ExpiresAt > now), ct);
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
                detail: $"Plugin '{pluginId}' is not active or enabled for your workspace.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(ctx);
    }
}
