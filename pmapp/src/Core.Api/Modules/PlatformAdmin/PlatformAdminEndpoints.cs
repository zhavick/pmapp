using Core.Api.Domain;
using Core.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Core.Api.Modules.PlatformAdmin;

public static class PlatformAdminEndpoints
{
    public sealed record GenerateLicenseRequest(string PluginId, string DurationType, decimal InvoiceAmount, string? Currency);
    public sealed record RejectRequest(string Reason);
    public sealed record ToggleGlobalPluginRequest(bool IsGloballyEnabled);
    public sealed record UpdateTenantStatusRequest(string Status);

    public static void MapPlatformAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/platform").RequireAuthorization("SuperAdminOnly");

        // Tenants
        group.MapGet("/tenants", async (AppDbContext db) =>
        {
            var tenants = await db.Tenants.OrderByDescending(t => t.CreatedAt).ToListAsync();
            var memberCounts = await db.TenantMemberships.IgnoreQueryFilters()
                .Where(m => !m.IsDeleted)
                .GroupBy(m => m.TenantId)
                .Select(g => new { TenantId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.TenantId, x => x.Count);

            return Results.Ok(tenants.Select(t => new
            {
                t.Id,
                t.Slug,
                t.CompanyName,
                t.Status,
                t.MaxUsersQuota,
                usersCount = memberCounts.TryGetValue(t.Id, out var c) ? c : 0,
                t.MaxStorageBytes,
                t.UsedStorageBytes,
                t.CreatedAt
            }));
        });

        group.MapPatch("/tenants/{id:guid}/status", async (Guid id, UpdateTenantStatusRequest req, AppDbContext db) =>
        {
            var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
            if (tenant is null) return Results.NotFound();
            tenant.Status = req.Status;
            await db.SaveChangesAsync();
            return Results.Ok(tenant);
        });

        // Plugin Catalog & Global Kill Switch
        group.MapGet("/plugins", async (AppDbContext db) =>
            Results.Ok(await db.PluginCatalog.OrderBy(p => p.Name).ToListAsync()));

        group.MapPatch("/plugins/{pluginId}/global-toggle", async (string pluginId, ToggleGlobalPluginRequest req, AppDbContext db) =>
        {
            var plugin = await db.PluginCatalog.FirstOrDefaultAsync(p => p.PluginId == pluginId);
            if (plugin is null) return Results.NotFound();
            plugin.IsGloballyEnabled = req.IsGloballyEnabled;
            await db.SaveChangesAsync();
            return Results.Ok(plugin);
        });

        // License Generator (Decision D11)
        group.MapGet("/licenses", async (AppDbContext db) =>
        {
            var licenses = await db.LicenseNumbers.OrderByDescending(l => l.CreatedAt).ToListAsync();
            return Results.Ok(licenses);
        });

        group.MapPost("/licenses/generate", async (GenerateLicenseRequest req, AppDbContext db) =>
        {
            var plugin = await db.PluginCatalog.FirstOrDefaultAsync(p => p.PluginId == req.PluginId);
            if (plugin is null)
                return Results.Problem("Plugin not found in catalog.", statusCode: StatusCodes.Status400BadRequest);

            var duration = req.DurationType is "OneMonth" or "OneYear" or "Lifetime" ? req.DurationType : "OneYear";
            var codePrefix = req.PluginId.ToUpperInvariant();
            if (codePrefix.Length > 4) codePrefix = codePrefix[..4];

            var seg1 = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
            var seg2 = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
            var key = $"PLG-{codePrefix}-{DateTime.UtcNow.Year}-{seg1}-{seg2}";

            var license = new LicenseNumber
            {
                Id = Guid.NewGuid(),
                LicenseKey = key,
                PluginId = req.PluginId,
                DurationType = duration,
                InvoiceAmount = req.InvoiceAmount >= 0 ? req.InvoiceAmount : plugin.DefaultPrice,
                Currency = string.IsNullOrWhiteSpace(req.Currency) ? "USD" : req.Currency.Trim().ToUpperInvariant(),
                Status = "Available"
            };

            db.LicenseNumbers.Add(license);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/platform/licenses/{license.Id}", license);
        });

        // License Approval Queue (Decision D9)
        group.MapGet("/license-requests", async (string? status, AppDbContext db) =>
        {
            var targetStatus = string.IsNullOrWhiteSpace(status) ? "PendingApproval" : status;
            var requests = await db.TenantPluginEntitlements
                .IgnoreQueryFilters()
                .Where(e => e.Status == targetStatus && !e.IsDeleted)
                .OrderByDescending(e => e.RequestedAt)
                .ToListAsync();

            var tenants = await db.Tenants.ToDictionaryAsync(t => t.Id, t => new { t.CompanyName, t.Slug });
            var plugins = await db.PluginCatalog.ToDictionaryAsync(p => p.PluginId, p => p.Name);
            var licenses = await db.LicenseNumbers.ToDictionaryAsync(l => l.Id, l => new { l.LicenseKey, l.DurationType });
            var invoices = await db.PlatformInvoices.ToDictionaryAsync(i => i.Id, i => new { i.InvoiceNumber, i.Amount, i.Currency, i.Status });
            var users = await db.Users.ToDictionaryAsync(u => u.Id, u => u.FullName);

            return Results.Ok(requests.Select(r =>
            {
                var t = tenants.GetValueOrDefault(r.TenantId);
                var lic = r.LicenseNumberId.HasValue ? licenses.GetValueOrDefault(r.LicenseNumberId.Value) : null;
                var inv = r.LatestInvoiceId.HasValue ? invoices.GetValueOrDefault(r.LatestInvoiceId.Value) : null;
                return new
                {
                    r.Id,
                    r.TenantId,
                    tenantName = t?.CompanyName ?? "",
                    tenantSlug = t?.Slug ?? "",
                    r.PluginId,
                    pluginName = plugins.GetValueOrDefault(r.PluginId, r.PluginId),
                    r.Status,
                    licenseKey = lic?.LicenseKey ?? "",
                    durationType = lic?.DurationType ?? "",
                    invoiceNumber = inv?.InvoiceNumber ?? "",
                    invoiceAmount = inv?.Amount ?? 0m,
                    invoiceCurrency = inv?.Currency ?? "USD",
                    invoiceStatus = inv?.Status ?? "",
                    requestedBy = r.RequestedByUserId.HasValue ? users.GetValueOrDefault(r.RequestedByUserId.Value, "") : "",
                    r.RequestedAt,
                    r.ActivatedAt,
                    r.ExpiresAt,
                    r.RejectionReason
                };
            }));
        });

        group.MapPost("/license-requests/{id:guid}/approve", async (Guid id, AppDbContext db) =>
        {
            var ent = await db.TenantPluginEntitlements
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
            if (ent is null) return Results.NotFound();

            var lic = ent.LicenseNumberId.HasValue
                ? await db.LicenseNumbers.FirstOrDefaultAsync(l => l.Id == ent.LicenseNumberId.Value)
                : null;

            var now = DateTimeOffset.UtcNow;
            ent.Status = "Active";
            ent.IsEnabledByTenant = true;
            ent.ActivatedAt = now;
            ent.RejectionReason = null;
            ent.ExpiresAt = lic?.DurationType switch
            {
                "OneMonth" => now.AddMonths(1),
                "OneYear" => now.AddYears(1),
                "Lifetime" => null,
                _ => now.AddYears(1)
            };

            await db.SaveChangesAsync();
            return Results.Ok(new { ent.Id, ent.Status, ent.ActivatedAt, ent.ExpiresAt });
        });

        group.MapPost("/license-requests/{id:guid}/reject", async (Guid id, RejectRequest req, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Reason))
                return Results.Problem("Rejection reason is required.", statusCode: StatusCodes.Status400BadRequest);

            var ent = await db.TenantPluginEntitlements
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
            if (ent is null) return Results.NotFound();

            ent.Status = "Rejected";
            ent.IsEnabledByTenant = false;
            ent.RejectionReason = req.Reason.Trim();

            await db.SaveChangesAsync();
            return Results.Ok(new { ent.Id, ent.Status, ent.RejectionReason });
        });

        group.MapGet("/invoices", async (AppDbContext db) =>
        {
            var invoices = await db.PlatformInvoices.OrderByDescending(i => i.PaidAt).ToListAsync();
            var tenants = await db.Tenants.ToDictionaryAsync(t => t.Id, t => t.CompanyName);
            return Results.Ok(invoices.Select(i => new
            {
                i.Id,
                i.InvoiceNumber,
                i.TenantId,
                tenantName = tenants.TryGetValue(i.TenantId, out var tn) ? tn : "",
                i.PluginId,
                i.Amount,
                i.Currency,
                i.Status,
                i.PaidAt
            }));
        });

        group.MapGet("/audit-logs", async (AppDbContext db) =>
            Results.Ok(await db.AuditLogs.OrderByDescending(a => a.CreatedAt).Take(100).ToListAsync()));
    }
}
