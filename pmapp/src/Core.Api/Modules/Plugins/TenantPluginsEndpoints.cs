using Core.Api.Domain;
using Core.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using PMApp.Plugin.Abstractions;

namespace Core.Api.Modules.Plugins;

public static class TenantPluginsEndpoints
{
    public sealed record RedeemLicenseRequest(string LicenseKey);
    public sealed record TogglePluginRequest(bool Enabled);

    public static void MapTenantPluginsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tenant/plugins").RequireAuthorization();

        group.MapGet("/", async (AppDbContext db, ITenantContext tenantContext) =>
        {
            var catalog = await db.PluginCatalog.OrderBy(p => p.Name).ToListAsync();
            var entitlements = await db.TenantPluginEntitlements.ToListAsync();
            var invoiceIds = entitlements.Where(e => e.LatestInvoiceId.HasValue).Select(e => e.LatestInvoiceId!.Value).ToList();
            var invoices = await db.PlatformInvoices
                .Where(i => invoiceIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, i => new { i.InvoiceNumber, i.Amount, i.Currency, i.Status });

            var licIds = entitlements.Where(e => e.LicenseNumberId.HasValue).Select(e => e.LicenseNumberId!.Value).ToList();
            var licenses = await db.LicenseNumbers
                .Where(l => licIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, l => new { l.LicenseKey, l.DurationType });

            var result = catalog.Select(p =>
            {
                var ent = entitlements.FirstOrDefault(e => e.PluginId == p.PluginId);
                var inv = ent?.LatestInvoiceId is Guid invId && invoices.TryGetValue(invId, out var foundInv) ? foundInv : null;
                var lic = ent?.LicenseNumberId is Guid licId && licenses.TryGetValue(licId, out var foundLic) ? foundLic : null;

                return new
                {
                    p.PluginId,
                    p.Name,
                    p.Description,
                    p.Version,
                    p.IsGloballyEnabled,
                    p.DefaultPrice,
                    status = ent?.Status ?? "NotLicensed",
                    isEnabledByTenant = ent?.IsEnabledByTenant ?? false,
                    activatedAt = ent?.ActivatedAt,
                    expiresAt = ent?.ExpiresAt,
                    rejectionReason = ent?.RejectionReason,
                    invoice = inv,
                    license = lic
                };
            });

            return Results.Ok(result);
        });

        group.MapPost("/redeem", async (RedeemLicenseRequest req, AppDbContext db, ITenantContext tenantContext) =>
        {
            if (string.IsNullOrWhiteSpace(req.LicenseKey))
                return Results.Problem("License key is required.", statusCode: StatusCodes.Status400BadRequest);

            if (tenantContext.CurrentTenantId == Guid.Empty || tenantContext.CurrentUserId is not Guid userId)
                return Results.Unauthorized();

            var keyNorm = req.LicenseKey.Trim().ToUpperInvariant();
            var license = await db.LicenseNumbers.FirstOrDefaultAsync(l => l.LicenseKey == keyNorm);
            if (license is null || license.Status != "Available")
            {
                return Results.Problem(
                    title: "Invalid or already redeemed license",
                    detail: "The license number was not found or has already been used.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            await using var tx = await db.Database.BeginTransactionAsync();

            var now = DateTimeOffset.UtcNow;
            license.Status = "Redeemed";
            license.RedeemedByTenantId = tenantContext.CurrentTenantId;
            license.RedeemedAt = now;

            var invSeq = await db.PlatformInvoices.CountAsync() + 401;
            var invoice = new PlatformInvoice
            {
                Id = Guid.NewGuid(),
                InvoiceNumber = $"INV-{invSeq:D4}-{Guid.NewGuid().ToString("N")[..3].ToUpperInvariant()}",
                TenantId = tenantContext.CurrentTenantId,
                LicenseNumberId = license.Id,
                PluginId = license.PluginId,
                Amount = license.InvoiceAmount,
                Currency = license.Currency,
                Status = "Paid",
                PaidAt = now
            };
            db.PlatformInvoices.Add(invoice);

            var ent = await db.TenantPluginEntitlements
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.TenantId == tenantContext.CurrentTenantId && e.PluginId == license.PluginId && !e.IsDeleted);

            if (ent is null)
            {
                ent = new TenantPluginEntitlement
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantContext.CurrentTenantId,
                    PluginId = license.PluginId
                };
                db.TenantPluginEntitlements.Add(ent);
            }

            ent.Status = "PendingApproval";
            ent.IsEnabledByTenant = false;
            ent.LicenseNumberId = license.Id;
            ent.LatestInvoiceId = invoice.Id;
            ent.RequestedByUserId = userId;
            ent.RequestedAt = now;
            ent.RejectionReason = null;

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return Results.Ok(new
            {
                entitlement = new { ent.Id, ent.PluginId, ent.Status, ent.RequestedAt },
                invoice = new { invoice.Id, invoice.InvoiceNumber, invoice.Amount, invoice.Currency, invoice.Status, invoice.PaidAt }
            });
        });

        group.MapPatch("/{pluginId}/toggle", async (string pluginId, TogglePluginRequest req, AppDbContext db) =>
        {
            var ent = await db.TenantPluginEntitlements.FirstOrDefaultAsync(e => e.PluginId == pluginId);
            if (ent is null || ent.Status != "Active")
            {
                return Results.Problem(
                    title: "Plugin not licensed",
                    detail: "Only approved Active plugins can be toggled.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            ent.IsEnabledByTenant = req.Enabled;
            await db.SaveChangesAsync();
            return Results.Ok(new { ent.PluginId, ent.Status, ent.IsEnabledByTenant });
        });
    }
}
