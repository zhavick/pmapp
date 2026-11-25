using Core.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Core.Api.Common;

[Authorize]
public sealed class WorkspaceHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId}");
        }
        await base.OnConnectedAsync();
    }
}

public sealed class PlatformMaintenanceWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<PlatformMaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("PM App BackgroundService initialized (license expiration watcher).");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = DateTimeOffset.UtcNow;

                var expired = await db.TenantPluginEntitlements
                    .IgnoreQueryFilters()
                    .Where(e => e.Status == "Active" && e.ExpiresAt != null && e.ExpiresAt <= now && !e.IsDeleted)
                    .ToListAsync(stoppingToken);

                if (expired.Count > 0)
                {
                    foreach (var item in expired)
                    {
                        item.Status = "Expired";
                        item.IsEnabledByTenant = false;
                    }
                    await db.SaveChangesAsync(stoppingToken);
                    logger.LogInformation("Soft-locked {Count} expired plugin entitlements.", expired.Count);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Error during license expiration check.");
            }

            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }
}
