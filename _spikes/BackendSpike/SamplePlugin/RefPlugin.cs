using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Spike.Abstractions;

namespace Spike.SamplePlugin;

public sealed class RefPlugin : IPlugin
{
    public string PluginId => "ref";

    public void ConfigureServices(IServiceCollection services)
    {
        // Plugin-specific services would go here
    }

    public void MapEndpoints(IEndpointRouteBuilder pluginGroup)
    {
        pluginGroup.MapGet("/ping", (ITenantContext tenant) =>
            Results.Ok(new
            {
                plugin = PluginId,
                tenantId = tenant.CurrentTenantId,
                message = "Reference plugin active"
            }));
    }
}
