using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PMApp.Plugin.Abstractions;

namespace ReferencePlugin;

public sealed class ReferencePluginEntry : IPlugin, IPluginDbMigration, IPluginPermissionProvider
{
    public PluginManifest Manifest => new(
        PluginId: "ref",
        Name: "Reference Sample Plugin",
        Version: "1.0.0",
        Description: "Sample plugin verifying SDK contracts, RLS migrations, RBAC, and UI slots.",
        RequiresCore: ">=1.0.0 <2.0.0",
        DefaultPrice: 99m);

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Plugin services registered here
    }

    public async Task ApplyMigrationsAsync(NpgsqlConnection adminConnection, CancellationToken ct = default)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS plugin_ref_items (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                name varchar(200) NOT NULL,
                created_at timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_plugin_ref_items_tenant ON plugin_ref_items (tenant_id);

            GRANT SELECT, INSERT, UPDATE, DELETE ON plugin_ref_items TO pmapp_app;

            ALTER TABLE plugin_ref_items ENABLE ROW LEVEL SECURITY;
            ALTER TABLE plugin_ref_items FORCE ROW LEVEL SECURITY;
            DROP POLICY IF EXISTS tenant_isolation_plugin_ref_items ON plugin_ref_items;
            CREATE POLICY tenant_isolation_plugin_ref_items ON plugin_ref_items
                USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
            """;

        await using var cmd = new NpgsqlCommand(sql, adminConnection);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public IReadOnlyList<PluginPermissionDefinition> GetPermissions() =>
    [
        new("ref.items.view", "View Reference Items", "Reference Plugin"),
        new("ref.items.manage", "Create Reference Items", "Reference Plugin")
    ];

    public void MapEndpoints(IEndpointRouteBuilder pluginGroup)
    {
        pluginGroup.MapGet("/items", async (ITenantContext tenant, IConfiguration config) =>
        {
            var connStr = config.GetConnectionString("Default")
                ?? "Host=localhost;Port=5432;Database=pmapp_dev;Username=pmapp_app;Password=pmapp_app_password";
            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();
            await using var setCmd = new NpgsqlCommand("SELECT set_config('app.current_tenant_id', @tid, false)", conn);
            setCmd.Parameters.AddWithValue("tid", tenant.CurrentTenantId.ToString());
            await setCmd.ExecuteNonQueryAsync();

            // Query WITHOUT WHERE tenant_id to prove PostgreSQL RLS enforces isolation on plugin tables
            await using var cmd = new NpgsqlCommand("SELECT id, tenant_id, name FROM plugin_ref_items ORDER BY created_at DESC", conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            var list = new List<object>();
            while (await reader.ReadAsync())
            {
                list.Add(new
                {
                    id = reader.GetGuid(0),
                    tenantId = reader.GetGuid(1),
                    name = reader.GetString(2)
                });
            }
            return Results.Ok(list);
        });

        pluginGroup.MapPost("/items", async (CreateRefItemRequest req, ITenantContext tenant, IConfiguration config) =>
        {
            var connStr = config.GetConnectionString("Default")
                ?? "Host=localhost;Port=5432;Database=pmapp_dev;Username=pmapp_app;Password=pmapp_app_password";
            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();
            await using var setCmd = new NpgsqlCommand("SELECT set_config('app.current_tenant_id', @tid, false)", conn);
            setCmd.Parameters.AddWithValue("tid", tenant.CurrentTenantId.ToString());
            await setCmd.ExecuteNonQueryAsync();

            var id = Guid.NewGuid();
            await using var cmd = new NpgsqlCommand(
                "INSERT INTO plugin_ref_items (id, tenant_id, name) VALUES (@id, @tid, @name)", conn);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("tid", tenant.CurrentTenantId);
            cmd.Parameters.AddWithValue("name", req.Name);
            await cmd.ExecuteNonQueryAsync();

            return Results.Created($"/api/v1/p/ref/items/{id}", new { id, tenantId = tenant.CurrentTenantId, name = req.Name });
        });
    }

    public sealed record CreateRefItemRequest(string Name);
}
