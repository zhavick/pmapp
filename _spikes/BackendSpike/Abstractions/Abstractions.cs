using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Spike.Abstractions;

public interface ITenantContext
{
    Guid CurrentTenantId { get; }
}

public interface IPluginEntitlementChecker
{
    Task<bool> IsPluginEnabledForTenantAsync(Guid tenantId, string pluginId, CancellationToken ct = default);
}

public interface IPlugin
{
    string PluginId { get; }
    void ConfigureServices(IServiceCollection services);
    void MapEndpoints(IEndpointRouteBuilder pluginGroup);
}
