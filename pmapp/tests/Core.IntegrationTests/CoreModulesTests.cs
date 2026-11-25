using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Core.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Core.IntegrationTests;

[Collection("Database")]
public class CoreModulesTests : IAsyncLifetime
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
            b.UseSetting("ConnectionStrings:Admin", AdminConnStr);
            b.UseSetting("ConnectionStrings:Default", AppConnStr);
        });
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Daily_PM_Flow_Progress_UndoDelete_And_Team_Works_EndToEnd()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var client = _factory.CreateClient();

        // 1. Register Tenant
        var regRes = await client.PostAsJsonAsync("/api/v1/auth/register-tenant", new
        {
            companyName = $"PM Flow Co {suffix}",
            fullName = "Rina PM",
            email = $"pm-{suffix}@test.local",
            password = "Password123!"
        });
        var auth = await regRes.Content.ReadFromJsonAsync<AuthRes>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // 2. Create Client, Delete, then Undo Restore
        var cRes = await client.PostAsJsonAsync("/api/v1/clients", new
        {
            companyName = "PT Maju Jaya",
            email = "info@majujaya.id",
            currency = "IDR"
        });
        Assert.Equal(HttpStatusCode.Created, cRes.StatusCode);
        var createdClient = await cRes.Content.ReadFromJsonAsync<IdDto>();
        Assert.NotNull(createdClient);

        var delClientRes = await client.DeleteAsync($"/api/v1/clients/{createdClient.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delClientRes.StatusCode);

        var restoreRes = await client.PostAsync($"/api/v1/clients/{createdClient.Id}/restore", null);
        Assert.Equal(HttpStatusCode.OK, restoreRes.StatusCode);

        // 3. Create Project + Milestone + 2 Tasks (2 pts + 3 pts = 5 pts)
        var pRes = await client.PostAsJsonAsync("/api/v1/projects", new
        {
            title = "Website Redesign",
            clientId = createdClient.Id,
            status = "InProgress"
        });
        Assert.Equal(HttpStatusCode.Created, pRes.StatusCode);
        var proj = await pRes.Content.ReadFromJsonAsync<IdDto>();
        Assert.NotNull(proj);

        var msRes = await client.PostAsJsonAsync($"/api/v1/projects/{proj.Id}/milestones", new
        {
            title = "Design Phase",
            sortOrder = 1
        });
        var ms = await msRes.Content.ReadFromJsonAsync<IdDto>();
        Assert.NotNull(ms);

        var t1Res = await client.PostAsJsonAsync($"/api/v1/projects/{proj.Id}/tasks", new
        {
            title = "Wireframes",
            points = 2,
            milestoneId = ms.Id
        });
        var t1 = await t1Res.Content.ReadFromJsonAsync<IdDto>();

        var t2Res = await client.PostAsJsonAsync($"/api/v1/projects/{proj.Id}/tasks", new
        {
            title = "UI Mockups",
            points = 3,
            milestoneId = ms.Id
        });
        var t2 = await t2Res.Content.ReadFromJsonAsync<IdDto>();

        // 4. Mark Task 1 Done -> Progress must be 40% (2 / 5 * 100)
        await client.PatchAsJsonAsync($"/api/v1/tasks/{t1!.Id}/status", new { status = "Done" });
        var detail1 = await client.GetFromJsonAsync<ProjectDetailDto>($"/api/v1/projects/{proj.Id}");
        Assert.Equal(40, detail1!.Progress);

        // 5. Batch Update Task 2 Done -> Progress must be 100%
        await client.PostAsJsonAsync("/api/v1/tasks/batch-update", new
        {
            taskIds = new[] { t2!.Id },
            status = "Done"
        });
        var detail2 = await client.GetFromJsonAsync<ProjectDetailDto>($"/api/v1/projects/{proj.Id}");
        Assert.Equal(100, detail2!.Progress);

        // 6. Dashboard KPIs
        var dash = await client.GetFromJsonAsync<DashboardDto>("/api/v1/workspace/dashboard");
        Assert.Equal(1, dash!.Kpis.ActiveProjects);
        Assert.Equal(0, dash.Kpis.OpenTasks);
        Assert.Equal(1, dash.Kpis.ClientsCount);
    }

    private sealed record AuthRes(string AccessToken);
    private sealed record IdDto(Guid Id);
    private sealed record ProjectDetailDto(int Progress);
    private sealed record DashboardDto(KpisDto Kpis);
    private sealed record KpisDto(int ActiveProjects, int OpenTasks, int ClientsCount);
}
