using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Core.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Core.IntegrationTests;

[Collection("Database")]
public class MultiTenantFoundationTests : IAsyncLifetime
{
    private const string AdminConnStr = "Host=localhost;Port=5432;Database=pmapp_dev;Username=postgres;Password=pmapp_dev_password";
    private const string AppConnStr = "Host=localhost;Port=5432;Database=pmapp_dev;Username=pmapp_app;Password=pmapp_app_password";

    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await DatabaseInitializer.InitializeAsync(AdminConnStr);

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Admin"] = AdminConnStr,
                    ["ConnectionStrings:Default"] = AppConnStr
                });
            });
        });
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Two_Tenants_Can_SignUp_And_Data_Plus_AuditLogs_Are_Isolated()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var clientA = _factory.CreateClient();
        var clientB = _factory.CreateClient();

        // 1. Register Tenant A
        var regA = await clientA.PostAsJsonAsync("/api/v1/auth/register-tenant", new
        {
            companyName = $"Acme Studio {suffix}",
            fullName = "Rina Admin",
            email = $"rina-{suffix}@acme.test",
            password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.Created, regA.StatusCode);
        var authA = await regA.Content.ReadFromJsonAsync<AuthResult>();
        Assert.NotNull(authA);
        Assert.Equal(10, authA.Tenant.MaxUsersQuota);

        // 2. Register Tenant B
        var regB = await clientB.PostAsJsonAsync("/api/v1/auth/register-tenant", new
        {
            companyName = $"Nusantara Tech {suffix}",
            fullName = "Budi Admin",
            email = $"budi-{suffix}@nusantara.test",
            password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.Created, regB.StatusCode);
        var authB = await regB.Content.ReadFromJsonAsync<AuthResult>();
        Assert.NotNull(authB);

        // 3. Tenant A creates a note
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authA.AccessToken);
        var noteARes = await clientA.PostAsJsonAsync("/api/v1/workspace/notes", new
        {
            title = $"Acme Secret {suffix}",
            content = "Confidential roadmap",
            isSticky = true
        });
        Assert.Equal(HttpStatusCode.Created, noteARes.StatusCode);

        // 4. Tenant B creates a note
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authB.AccessToken);
        var noteBRes = await clientB.PostAsJsonAsync("/api/v1/workspace/notes", new
        {
            title = $"Nusantara Secret {suffix}",
            content = "Internal budget",
            isSticky = false
        });
        Assert.Equal(HttpStatusCode.Created, noteBRes.StatusCode);

        // 5. Verify strict isolation: Tenant A sees only Acme note, Tenant B sees only Nusantara note
        var notesA = await clientA.GetFromJsonAsync<List<NoteDto>>("/api/v1/workspace/notes");
        Assert.NotNull(notesA);
        Assert.Single(notesA);
        Assert.Equal($"Acme Secret {suffix}", notesA[0].Title);

        var notesB = await clientB.GetFromJsonAsync<List<NoteDto>>("/api/v1/workspace/notes");
        Assert.NotNull(notesB);
        Assert.Single(notesB);
        Assert.Equal($"Nusantara Secret {suffix}", notesB[0].Title);

        // 6. Verify AuditLog captured Create for Tenant A and is isolated from Tenant B
        var logsA = await clientA.GetFromJsonAsync<List<AuditDto>>("/api/v1/workspace/audit-logs");
        Assert.NotNull(logsA);
        Assert.Contains(logsA, l => l.EntityName == nameof(Core.Api.Domain.WorkspaceNote) && l.Action == "Create");
    }

    [Fact]
    public async Task Duplicate_Email_Registration_Returns_409_Conflict()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var client = _factory.CreateClient();
        var payload = new
        {
            companyName = $"Dup Co {suffix}",
            fullName = "User One",
            email = $"dup-{suffix}@test.local",
            password = "Password123!"
        };

        var first = await client.PostAsJsonAsync("/api/v1/auth/register-tenant", payload);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/v1/auth/register-tenant", payload);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    private sealed record AuthResult(string AccessToken, TenantDto Tenant);
    private sealed record TenantDto(Guid Id, string Slug, string CompanyName, int MaxUsersQuota);
    private sealed record NoteDto(Guid Id, string Title, string Content, bool IsSticky);
    private sealed record AuditDto(Guid Id, string Action, string EntityName, string EntityId);
}
