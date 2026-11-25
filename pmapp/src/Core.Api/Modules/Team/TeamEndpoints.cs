using Core.Api.Domain;
using Core.Api.Infrastructure.Auth;
using Core.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using PMApp.Plugin.Abstractions;

namespace Core.Api.Modules.Team;

public static class TeamEndpoints
{
    public sealed record AddTeamMemberRequest(string FullName, string Email, string Password, Guid RoleId);
    public sealed record UpdateMemberRoleRequest(Guid RoleId);
    public sealed record CreateRoleRequest(string Name);
    public sealed record UpdateRolePermissionsRequest(List<PermissionItem> Permissions);
    public sealed record PermissionItem(string Key, string? PluginId);

    public static void MapTeamEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/team").RequireAuthorization();

        group.MapGet("/", async (AppDbContext db, ITenantContext tenantContext) =>
        {
            var tenant = await db.Tenants.FirstAsync(t => t.Id == tenantContext.CurrentTenantId);
            var members = await (from m in db.TenantMemberships
                                 join u in db.Users on m.UserId equals u.Id
                                 join r in db.Roles on m.RoleId equals r.Id
                                 orderby m.JoinedAt
                                 select new
                                 {
                                     membershipId = m.Id,
                                     userId = u.Id,
                                     u.FullName,
                                     u.Email,
                                     roleId = r.Id,
                                     roleName = r.Name,
                                     m.IsOwner,
                                     m.JoinedAt
                                 }).ToListAsync();

            return Results.Ok(new
            {
                maxUsersQuota = tenant.MaxUsersQuota,
                currentUsers = members.Count,
                members
            });
        });

        group.MapPost("/members", async (AddTeamMemberRequest req, AppDbContext db, ITenantContext tenantContext) =>
        {
            var tenant = await db.Tenants.FirstAsync(t => t.Id == tenantContext.CurrentTenantId);
            var currentCount = await db.TenantMemberships.CountAsync();
            if (currentCount >= tenant.MaxUsersQuota)
            {
                return Results.Problem(
                    title: "User quota reached",
                    detail: $"Your Free Core plan allows up to {tenant.MaxUsersQuota} team members.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var roleExists = await db.Roles.AnyAsync(r => r.Id == req.RoleId);
            if (!roleExists)
                return Results.Problem("Role not found.", statusCode: StatusCodes.Status400BadRequest);

            var emailNorm = req.Email.Trim().ToLowerInvariant();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == emailNorm);
            if (user is null)
            {
                if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
                    return Results.Problem("Password (min 6 chars) is required for new users.", statusCode: StatusCodes.Status400BadRequest);

                user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = emailNorm,
                    FullName = req.FullName.Trim(),
                    PasswordHash = PasswordHasher.Hash(req.Password),
                    LastActiveTenantId = tenant.Id
                };
                db.Users.Add(user);
            }
            else if (await db.TenantMemberships.AnyAsync(m => m.UserId == user.Id))
            {
                return Results.Problem("User is already a member of this workspace.", statusCode: StatusCodes.Status409Conflict);
            }

            var membership = new TenantMembership
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                UserId = user.Id,
                RoleId = req.RoleId,
                IsOwner = false
            };

            db.TenantMemberships.Add(membership);
            await db.SaveChangesAsync();

            return Results.Created($"/api/v1/team/members/{membership.Id}", new
            {
                membershipId = membership.Id,
                userId = user.Id,
                user.FullName,
                user.Email,
                membership.RoleId
            });
        });

        group.MapPatch("/members/{membershipId:guid}/role", async (Guid membershipId, UpdateMemberRoleRequest req, AppDbContext db) =>
        {
            var membership = await db.TenantMemberships.FirstOrDefaultAsync(m => m.Id == membershipId);
            if (membership is null) return Results.NotFound();
            if (!await db.Roles.AnyAsync(r => r.Id == req.RoleId)) return Results.BadRequest();

            membership.RoleId = req.RoleId;
            await db.SaveChangesAsync();
            return Results.Ok(new { membership.Id, membership.RoleId });
        });

        // Role Management
        app.MapPost("/api/v1/rbac/roles", async (CreateRoleRequest req, AppDbContext db, ITenantContext tenantContext) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest();
            var role = new Role
            {
                Id = Guid.NewGuid(),
                TenantId = tenantContext.CurrentTenantId,
                Name = req.Name.Trim(),
                IsSystem = false
            };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/rbac/roles/{role.Id}", role);
        }).RequireAuthorization();

        app.MapGet("/api/v1/rbac/roles/{roleId:guid}/permissions", async (Guid roleId, AppDbContext db) =>
        {
            var perms = await db.RolePermissions
                .Where(rp => rp.RoleId == roleId)
                .Select(rp => new { rp.PermissionKey, rp.PluginId })
                .ToListAsync();
            return Results.Ok(perms);
        }).RequireAuthorization();

        app.MapPut("/api/v1/rbac/roles/{roleId:guid}/permissions", async (
            Guid roleId,
            UpdateRolePermissionsRequest req,
            AppDbContext db,
            ITenantContext tenantContext) =>
        {
            if (!await db.Roles.AnyAsync(r => r.Id == roleId)) return Results.NotFound();

            var existing = await db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync();
            foreach (var item in existing)
            {
                item.IsDeleted = true;
                item.DeletedAt = DateTimeOffset.UtcNow;
            }

            foreach (var p in req.Permissions)
            {
                db.RolePermissions.Add(new RolePermission
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantContext.CurrentTenantId,
                    RoleId = roleId,
                    PermissionKey = p.Key,
                    PluginId = p.PluginId
                });
            }

            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization();
    }
}
