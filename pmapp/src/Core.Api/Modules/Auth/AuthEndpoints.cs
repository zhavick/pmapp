using System.Text.RegularExpressions;
using Core.Api.Domain;
using Core.Api.Infrastructure.Auth;
using Core.Api.Infrastructure.Persistence;
using Core.Api.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using PMApp.Plugin.Abstractions;

namespace Core.Api.Modules.Auth;

public static class AuthEndpoints
{
    public sealed record RegisterTenantRequest(string CompanyName, string FullName, string Email, string Password);
    public sealed record LoginRequest(string Email, string Password);
    public sealed record SwitchTenantRequest(Guid TargetTenantId);

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth");

        group.MapPost("/register-tenant", async (
            RegisterTenantRequest req,
            AppDbContext db,
            HttpTenantContext tenantContext,
            JwtTokenService jwt) =>
        {
            if (string.IsNullOrWhiteSpace(req.CompanyName) ||
                string.IsNullOrWhiteSpace(req.FullName) ||
                string.IsNullOrWhiteSpace(req.Email) ||
                string.IsNullOrWhiteSpace(req.Password) ||
                req.Password.Length < 6)
            {
                return Results.Problem(
                    title: "Invalid registration data",
                    detail: "Company name, full name, email, and password (min 6 chars) are required.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var emailNorm = req.Email.Trim().ToLowerInvariant();
            if (await db.Users.AnyAsync(u => u.Email == emailNorm))
            {
                return Results.Problem(
                    title: "Email already registered",
                    detail: "An account with this email already exists.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            var baseSlug = Regex.Replace(req.CompanyName.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
            if (string.IsNullOrWhiteSpace(baseSlug)) baseSlug = "workspace";
            var slug = $"{baseSlug}-{Guid.NewGuid().ToString("N")[..4]}";

            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Slug = slug,
                CompanyName = req.CompanyName.Trim(),
                Status = "Active",
                MaxUsersQuota = 10,
                MaxStorageBytes = 2L * 1024 * 1024 * 1024
            };

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = emailNorm,
                PasswordHash = PasswordHasher.Hash(req.Password),
                FullName = req.FullName.Trim(),
                IsSuperAdmin = false,
                LastActiveTenantId = tenant.Id
            };

            await using var tx = await db.Database.BeginTransactionAsync();

            // Set tenant scope & update RLS session setting on the active connection
            tenantContext.SetOverride(tenant.Id, user.Id);
            await db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.current_tenant_id', {0}, true)",
                tenant.Id.ToString());

            db.Tenants.Add(tenant);
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var adminRole = new Role
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = "TenantAdmin",
                IsSystem = true
            };

            var memberRole = new Role
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = "Member",
                IsSystem = true
            };

            db.Roles.AddRange(adminRole, memberRole);
            await db.SaveChangesAsync();

            var membership = new TenantMembership
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                UserId = user.Id,
                RoleId = adminRole.Id,
                IsOwner = true
            };

            db.TenantMemberships.Add(membership);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            var token = jwt.GenerateAccessToken(user, tenant, adminRole.Name, Array.Empty<string>());
            return Results.Created($"/api/v1/tenants/{tenant.Id}", new
            {
                accessToken = token,
                user = new { user.Id, user.Email, user.FullName, user.IsSuperAdmin },
                tenant = new { tenant.Id, tenant.Slug, tenant.CompanyName, tenant.MaxUsersQuota, tenant.MaxStorageBytes },
                role = adminRole.Name,
                activePlugins = Array.Empty<string>()
            });
        });

        group.MapPost("/login", async (
            LoginRequest req,
            AppDbContext db,
            HttpTenantContext tenantContext,
            JwtTokenService jwt) =>
        {
            var emailNorm = req.Email.Trim().ToLowerInvariant();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == emailNorm);
            if (user is null || !PasswordHasher.Verify(req.Password, user.PasswordHash))
            {
                return Results.Problem(
                    title: "Invalid credentials",
                    detail: "Email or password is incorrect.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            // Pick target tenant (LastActiveTenantId or first membership)
            var memberships = await db.TenantMemberships
                .IgnoreQueryFilters()
                .Where(m => m.UserId == user.Id && !m.IsDeleted)
                .ToListAsync();

            if (memberships.Count == 0)
            {
                if (user.IsSuperAdmin)
                {
                    var saToken = jwt.GenerateAccessToken(user, null, "SuperAdmin", Array.Empty<string>());
                    return Results.Ok(new
                    {
                        accessToken = saToken,
                        user = new { user.Id, user.Email, user.FullName, user.IsSuperAdmin },
                        tenant = (object?)null,
                        role = "SuperAdmin",
                        activePlugins = Array.Empty<string>()
                    });
                }

                return Results.Problem(
                    title: "No workspace",
                    detail: "User does not belong to any active tenant workspace.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var activeMembership = memberships.FirstOrDefault(m => m.TenantId == user.LastActiveTenantId)
                                   ?? memberships[0];

            var tenant = await db.Tenants.FirstAsync(t => t.Id == activeMembership.TenantId);
            if (tenant.Status != "Active")
            {
                return Results.Problem(
                    title: "Workspace suspended",
                    detail: "This company workspace has been suspended.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            tenantContext.SetOverride(tenant.Id, user.Id);
            await db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.current_tenant_id', {0}, false)",
                tenant.Id.ToString());
            var role = await db.Roles
                .IgnoreQueryFilters()
                .FirstAsync(r => r.Id == activeMembership.RoleId);

            var activePlugins = await db.TenantPluginEntitlements
                .IgnoreQueryFilters()
                .Where(e => e.TenantId == tenant.Id && e.Status == "Active" && e.IsEnabledByTenant && !e.IsDeleted)
                .Select(e => e.PluginId)
                .ToListAsync();

            var token = jwt.GenerateAccessToken(user, tenant, role.Name, activePlugins);
            return Results.Ok(new
            {
                accessToken = token,
                user = new { user.Id, user.Email, user.FullName, user.IsSuperAdmin },
                tenant = new { tenant.Id, tenant.Slug, tenant.CompanyName, tenant.MaxUsersQuota, tenant.MaxStorageBytes },
                role = role.Name,
                activePlugins
            });
        });

        group.MapPost("/switch-tenant", async (
            SwitchTenantRequest req,
            AppDbContext db,
            HttpTenantContext tenantContext,
            JwtTokenService jwt) =>
        {
            if (tenantContext.CurrentUserId is not Guid userId)
                return Results.Unauthorized();

            var membership = await db.TenantMemberships
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.UserId == userId && m.TenantId == req.TargetTenantId && !m.IsDeleted);

            if (membership is null)
                return Results.Forbid();

            var user = await db.Users.FirstAsync(u => u.Id == userId);
            var tenant = await db.Tenants.FirstAsync(t => t.Id == req.TargetTenantId);
            var role = await db.Roles.IgnoreQueryFilters().FirstAsync(r => r.Id == membership.RoleId);

            user.LastActiveTenantId = tenant.Id;
            await db.SaveChangesAsync();

            var activePlugins = await db.TenantPluginEntitlements
                .IgnoreQueryFilters()
                .Where(e => e.TenantId == tenant.Id && e.Status == "Active" && e.IsEnabledByTenant && !e.IsDeleted)
                .Select(e => e.PluginId)
                .ToListAsync();

            var token = jwt.GenerateAccessToken(user, tenant, role.Name, activePlugins);
            return Results.Ok(new
            {
                accessToken = token,
                tenant = new { tenant.Id, tenant.Slug, tenant.CompanyName, tenant.MaxUsersQuota, tenant.MaxStorageBytes },
                role = role.Name,
                activePlugins
            });
        }).RequireAuthorization();

        group.MapGet("/me", async (AppDbContext db, ITenantContext tenantContext) =>
        {
            if (tenantContext.CurrentUserId is not Guid userId)
                return Results.Unauthorized();

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null) return Results.Unauthorized();

            var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantContext.CurrentTenantId);
            var workspaces = await (from m in db.TenantMemberships.IgnoreQueryFilters()
                                    join t in db.Tenants on m.TenantId equals t.Id
                                    where m.UserId == userId && !m.IsDeleted
                                    select new { t.Id, t.Slug, t.CompanyName }).ToListAsync();

            var activePlugins = await db.TenantPluginEntitlements
                .Where(e => e.Status == "Active" && e.IsEnabledByTenant)
                .Select(e => e.PluginId)
                .ToListAsync();

            return Results.Ok(new
            {
                user = new { user.Id, user.Email, user.FullName, user.IsSuperAdmin },
                tenant = tenant is null ? null : new { tenant.Id, tenant.Slug, tenant.CompanyName, tenant.MaxUsersQuota, tenant.MaxStorageBytes, tenant.UsedStorageBytes },
                workspaces,
                activePlugins
            });
        }).RequireAuthorization();
    }
}
