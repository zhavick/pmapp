using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Core.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace Core.IntegrationTests;

[Collection("Database")]
public class PluginEngineTests : IAsyncLifetime
{
    private const string AdminConnStr = "Host=localhost;Port=5432;Database=pmapp_dev;Username=postgres;Password=pmapp_dev_password";
    private const string AppConnStr = "Host=localhost;Port=5432;Database=pmapp_dev;Username=pmapp_app;Password=pmapp_app_password";

    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await DatabaseInitializer.InitializeAsync(AdminConnStr);

        var pluginDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "ReferencePlugin", "bin", "Debug", "net8.0"));
        Assert.True(Directory.Exists(pluginDir), $"ReferencePlugin output not found at {pluginDir}");

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.UseSetting("ConnectionStrings:Admin", AdminConnStr);
            b.UseSetting("ConnectionStrings:Default", AppConnStr);
            b.UseSetting("Plugins:Directory", pluginDir);
        });
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Plugin_Enforces_Entitlement_RBAC_Bundle_And_RLS_Isolation()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var clientA = _factory.CreateClient();
        var clientB = _factory.CreateClient();

        // 1. Register Tenant A and Tenant B
        var regA = await clientA.PostAsJsonAsync("/api/v1/auth/register-tenant", new
        {
            companyName = $"PluginCo A {suffix}",
            fullName = "Admin A",
            email = $"pa-{suffix}@test.local",
            password = "Password123!"
        });
        var authA = await regA.Content.ReadFromJsonAsync<AuthRes>();
        Assert.NotNull(authA);

        var regB = await clientB.PostAsJsonAsync("/api/v1/auth/register-tenant", new
        {
            companyName = $"PluginCo B {suffix}",
            fullName = "Admin B",
            email = $"pb-{suffix}@test.local",
            password = "Password123!"
        });
        var authB = await regB.Content.ReadFromJsonAsync<AuthRes>();
        Assert.NotNull(authB);

        // 2. Grant 'ref' Active + Enabled to Tenant A; leave Tenant B without entitlement
        await using (var conn = new NpgsqlConnection(AdminConnStr))
        {
            await conn.OpenAsync();
            const string grantSql = """
                INSERT INTO tenant_plugin_entitlements (id, tenant_id, plugin_id, status, is_enabled_by_tenant, activated_at)
                VALUES (@id, @tid, 'ref', 'Active', true, now())
                ON CONFLICT (tenant_id, plugin_id) DO UPDATE SET status = 'Active', is_enabled_by_tenant = true;
                """;
            await using var cmd = new NpgsqlCommand(grantSql, conn);
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("tid", authA.Tenant.Id);
            await cmd.ExecuteNonQueryAsync();
        }

        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authA.AccessToken);
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authB.AccessToken);

        // 3. Tenant A can call plugin API; Tenant B gets 403 Forbidden
        var createA = await clientA.PostAsJsonAsync("/api/v1/p/ref/items", new { name = $"ItemA-{suffix}" });
        Assert.Equal(HttpStatusCode.Created, createA.StatusCode);

        var listA = await clientA.GetFromJsonAsync<List<RefItemDto>>("/api/v1/p/ref/items");
        Assert.NotNull(listA);
        Assert.Contains(listA, i => i.Name == $"ItemA-{suffix}");

        var listBRes = await clientB.GetAsync("/api/v1/p/ref/items");
        Assert.Equal(HttpStatusCode.Forbidden, listBRes.StatusCode);

        // 4. Dynamic RBAC: Tenant A sees 'ref.items.view'; Tenant B does NOT
        var permsA = await clientA.GetFromJsonAsync<List<PermDto>>("/api/v1/rbac/permissions");
        var permsB = await clientB.GetFromJsonAsync<List<PermDto>>("/api/v1/rbac/permissions");
        Assert.Contains(permsA!, p => p.Key == "ref.items.view");
        Assert.DoesNotContain(permsB!, p => p.Key == "ref.items.view");

        // 5. Frontend Extensions API & bundle serving
        var extA = await clientA.GetFromJsonAsync<List<ExtDto>>("/api/v1/extensions/active");
        var extB = await clientB.GetFromJsonAsync<List<ExtDto>>("/api/v1/extensions/active");
        Assert.Contains(extA!, e => e.PluginId == "ref" && e.BundleUrl == "/plugins/ref/index.es.js");
        Assert.DoesNotContain(extB!, e => e.PluginId == "ref");

        var bundleRes = await clientA.GetAsync("/plugins/ref/index.es.js");
        Assert.Equal(HttpStatusCode.OK, bundleRes.StatusCode);
    }

    private sealed record AuthRes(string AccessToken, TenantDto Tenant);
    private sealed record TenantDto(Guid Id);
    private sealed record RefItemDto(Guid Id, Guid TenantId, string Name);
    private sealed record PermDto(string Key, string? PluginId);
    private sealed record ExtDto(string PluginId, string BundleUrl);
}
