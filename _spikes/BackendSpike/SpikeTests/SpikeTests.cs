using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace SpikeTests;

public class MultiTenantAndPluginSpikes : IAsyncLifetime
{
    private const string AdminConnStr = "Host=localhost;Port=5439;Database=pmapp_spike;Username=postgres;Password=spike_pass";
    private const string AppConnStr = "Host=localhost;Port=5439;Database=pmapp_spike;Username=pmapp_app;Password=app_pass";

    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await using var conn = new NpgsqlConnection(AdminConnStr);
        await conn.OpenAsync();

        var sql = $"""
            DO $$ BEGIN
                IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'pmapp_app') THEN
                    CREATE ROLE pmapp_app LOGIN PASSWORD 'app_pass' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
                END IF;
            END $$;

            DROP TABLE IF EXISTS projects;
            DROP TABLE IF EXISTS tenant_plugin_entitlements;

            CREATE TABLE projects (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                title text NOT NULL
            );

            CREATE TABLE tenant_plugin_entitlements (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                plugin_id text NOT NULL,
                status text NOT NULL
            );

            GRANT SELECT, INSERT, UPDATE, DELETE ON projects, tenant_plugin_entitlements TO pmapp_app;

            ALTER TABLE projects ENABLE ROW LEVEL SECURITY;
            ALTER TABLE projects FORCE ROW LEVEL SECURITY;
            CREATE POLICY tenant_isolation_projects ON projects
                USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);

            INSERT INTO projects (id, tenant_id, title) VALUES
                ('11111111-1111-1111-1111-111111111111', '{TenantA}', 'Tenant A Secret Project'),
                ('22222222-2222-2222-2222-222222222222', '{TenantB}', 'Tenant B Secret Project');

            INSERT INTO tenant_plugin_entitlements (id, tenant_id, plugin_id, status) VALUES
                ('33333333-3333-3333-3333-333333333333', '{TenantA}', 'ref', 'Active'),
                ('44444444-4444-4444-4444-444444444444', '{TenantB}', 'ref', 'PendingApproval');
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();

        var pluginOutDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "SamplePlugin", "bin", "Debug", "net8.0"));
        Assert.True(Directory.Exists(pluginOutDir), $"Missing plugin dir: {pluginOutDir}");

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("PluginDir", pluginOutDir);
            b.UseSetting("ConnectionStrings:Default", AppConnStr);
        });
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task S1_EfCore_And_RawSql_Are_Isolated_By_PostgreSql_RLS()
    {
        var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Add("X-Tenant-Id", TenantA.ToString());

        var efProjectsA = await clientA.GetFromJsonAsync<List<ProjectDto>>("/api/v1/projects");
        Assert.NotNull(efProjectsA);
        Assert.Single(efProjectsA);
        Assert.Equal("Tenant A Secret Project", efProjectsA[0].Title);

        // Raw SQL has NO WHERE clause; PostgreSQL RLS must still block Tenant B rows!
        var rawProjectsA = await clientA.GetFromJsonAsync<List<ProjectDto>>("/api/v1/projects/raw");
        Assert.NotNull(rawProjectsA);
        Assert.Single(rawProjectsA);
        Assert.Equal("Tenant A Secret Project", rawProjectsA[0].Title);

        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("X-Tenant-Id", TenantB.ToString());

        var rawProjectsB = await clientB.GetFromJsonAsync<List<ProjectDto>>("/api/v1/projects/raw");
        Assert.NotNull(rawProjectsB);
        Assert.Single(rawProjectsB);
        Assert.Equal("Tenant B Secret Project", rawProjectsB[0].Title);
    }

    [Fact]
    public async Task S2_Plugin_Loaded_At_Startup_Enforces_Tenant_Entitlement()
    {
        var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Add("X-Tenant-Id", TenantA.ToString());
        var resA = await clientA.GetAsync("/api/v1/p/ref/ping");
        Assert.Equal(HttpStatusCode.OK, resA.StatusCode);

        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("X-Tenant-Id", TenantB.ToString());
        var resB = await clientB.GetAsync("/api/v1/p/ref/ping");
        Assert.Equal(HttpStatusCode.Forbidden, resB.StatusCode);
    }

    private sealed record ProjectDto(Guid Id, Guid TenantId, string Title);
}
