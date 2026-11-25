using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Core.Api.Infrastructure.Auth;
using Core.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace Core.IntegrationTests;

[Collection("Database")]
public class LicensingFlowTests : IAsyncLifetime
{
    private const string AdminConnStr = "Host=localhost;Port=5432;Database=pmapp_dev;Username=postgres;Password=pmapp_dev_password";
    private const string AppConnStr = "Host=localhost;Port=5432;Database=pmapp_dev;Username=pmapp_app;Password=pmapp_app_password";

    private WebApplicationFactory<Program> _factory = null!;
    private string _superAdminEmail = "";

    public async Task InitializeAsync()
    {
        await DatabaseInitializer.InitializeAsync(AdminConnStr);

        var suffix = Guid.NewGuid().ToString("N")[..6];
        _superAdminEmail = $"super-{suffix}@pmapp.io";

        await using (var conn = new NpgsqlConnection(AdminConnStr))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "INSERT INTO users (id, email, password_hash, full_name, is_super_admin) VALUES (@id, @email, @hash, 'Super Admin', true)",
                conn);
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("email", _superAdminEmail);
            cmd.Parameters.AddWithValue("hash", PasswordHasher.Hash("Super123!"));
            await cmd.ExecuteNonQueryAsync();
        }

        var pluginDir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "ReferencePlugin", "bin", "Debug", "net8.0"));

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
    public async Task Full_Licensing_Flow_Generate_Redeem_InvoicePaid_Approve_And_Toggle()
    {
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var superClient = _factory.CreateClient();
        var tenantClient = _factory.CreateClient();

        // 1. Super Admin logs in
        var saLogin = await superClient.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = _superAdminEmail,
            password = "Super123!"
        });
        Assert.Equal(HttpStatusCode.OK, saLogin.StatusCode);
        var saAuth = await saLogin.Content.ReadFromJsonAsync<AuthRes>();
        superClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", saAuth!.AccessToken);

        // 2. Tenant registers
        var reg = await tenantClient.PostAsJsonAsync("/api/v1/auth/register-tenant", new
        {
            companyName = $"LicCo {suffix}",
            fullName = "Rina Tenant",
            email = $"lic-{suffix}@test.local",
            password = "Password123!"
        });
        var tAuth = await reg.Content.ReadFromJsonAsync<AuthRes>();
        tenantClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tAuth!.AccessToken);

        // Tenant cannot access platform admin endpoints
        var forbiddenPlatform = await tenantClient.GetAsync("/api/v1/platform/tenants");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenPlatform.StatusCode);

        // 3. Super Admin generates a 1-year license key for 'ref'
        var genRes = await superClient.PostAsJsonAsync("/api/v1/platform/licenses/generate", new
        {
            pluginId = "ref",
            durationType = "OneYear",
            invoiceAmount = 149m,
            currency = "USD"
        });
        Assert.Equal(HttpStatusCode.Created, genRes.StatusCode);
        var lic = await genRes.Content.ReadFromJsonAsync<LicRes>();
        Assert.NotNull(lic);
        Assert.StartsWith("PLG-REF-", lic.LicenseKey);

        // 4. Tenant redeems license -> Paid Invoice + PendingApproval
        var redeemRes = await tenantClient.PostAsJsonAsync("/api/v1/tenant/plugins/redeem", new
        {
            licenseKey = lic.LicenseKey
        });
        Assert.Equal(HttpStatusCode.OK, redeemRes.StatusCode);
        var redeemBody = await redeemRes.Content.ReadFromJsonAsync<RedeemRes>();
        Assert.Equal("PendingApproval", redeemBody!.Entitlement.Status);
        Assert.Equal("Paid", redeemBody.Invoice.Status);

        // While PendingApproval, plugin API must return 403 Forbidden
        var pendingAccess = await tenantClient.GetAsync("/api/v1/p/ref/items");
        Assert.Equal(HttpStatusCode.Forbidden, pendingAccess.StatusCode);

        // 5. Super Admin sees request in queue and Approves it
        var queue = await superClient.GetFromJsonAsync<List<ReqDto>>("/api/v1/platform/license-requests?status=PendingApproval");
        Assert.Contains(queue!, r => r.Id == redeemBody.Entitlement.Id && r.InvoiceStatus == "Paid");

        var approveRes = await superClient.PostAsync($"/api/v1/platform/license-requests/{redeemBody.Entitlement.Id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approveRes.StatusCode);

        // 6. Now Tenant can access plugin API!
        var activeAccess = await tenantClient.GetAsync("/api/v1/p/ref/items");
        Assert.Equal(HttpStatusCode.OK, activeAccess.StatusCode);

        // 7. Tenant Admin disables plugin via toggle -> API returns 403 Forbidden again
        var toggleOff = await tenantClient.PatchAsJsonAsync("/api/v1/tenant/plugins/ref/toggle", new { enabled = false });
        Assert.Equal(HttpStatusCode.OK, toggleOff.StatusCode);

        var disabledAccess = await tenantClient.GetAsync("/api/v1/p/ref/items");
        Assert.Equal(HttpStatusCode.Forbidden, disabledAccess.StatusCode);
    }

    private sealed record AuthRes(string AccessToken);
    private sealed record LicRes(Guid Id, string LicenseKey);
    private sealed record RedeemRes(EntDto Entitlement, InvDto Invoice);
    private sealed record EntDto(Guid Id, string Status);
    private sealed record InvDto(Guid Id, string InvoiceNumber, string Status);
    private sealed record ReqDto(Guid Id, string InvoiceStatus);
}
