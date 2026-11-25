using Core.Api.Domain;
using Core.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using PMApp.Plugin.Abstractions;

namespace Core.Api.Modules.Workspace;

public static class WorkspaceEndpoints
{
    public sealed record CreateNoteRequest(string Title, string Content, bool IsSticky);
    public sealed record CreateTodoRequest(string Title, DateOnly? DueDate);
    public sealed record ToggleTodoRequest(bool IsCompleted);
    public sealed record UpdateTenantSettingsRequest(string CompanyName);

    public static void MapWorkspaceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/workspace").RequireAuthorization();

        // Notes
        group.MapGet("/notes", async (AppDbContext db) =>
        {
            var notes = await db.WorkspaceNotes
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new { n.Id, n.Title, n.Content, n.IsSticky, n.CreatedAt })
                .ToListAsync();
            return Results.Ok(notes);
        });

        group.MapPost("/notes", async (CreateNoteRequest req, AppDbContext db, ITenantContext tenantContext) =>
        {
            if (tenantContext.CurrentUserId is not Guid userId || tenantContext.CurrentTenantId == Guid.Empty)
                return Results.Unauthorized();

            var note = new WorkspaceNote
            {
                Id = Guid.NewGuid(),
                TenantId = tenantContext.CurrentTenantId,
                AuthorUserId = userId,
                Title = req.Title.Trim(),
                Content = req.Content,
                IsSticky = req.IsSticky
            };

            db.WorkspaceNotes.Add(note);
            await db.SaveChangesAsync();

            return Results.Created($"/api/v1/workspace/notes/{note.Id}", new
            {
                note.Id,
                note.Title,
                note.Content,
                note.IsSticky,
                note.CreatedAt
            });
        });

        group.MapDelete("/notes/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var note = await db.WorkspaceNotes.FirstOrDefaultAsync(n => n.Id == id);
            if (note is null) return Results.NotFound();

            note.IsDeleted = true;
            note.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Personal To-Do
        group.MapGet("/todos", async (AppDbContext db, ITenantContext tenantContext) =>
        {
            var todos = await db.TodoItems
                .Where(t => t.UserId == tenantContext.CurrentUserId)
                .OrderBy(t => t.IsCompleted)
                .ThenByDescending(t => t.CreatedAt)
                .Select(t => new { t.Id, t.Title, t.IsCompleted, t.DueDate, t.CreatedAt })
                .ToListAsync();
            return Results.Ok(todos);
        });

        group.MapPost("/todos", async (CreateTodoRequest req, AppDbContext db, ITenantContext tenantContext) =>
        {
            if (tenantContext.CurrentUserId is not Guid userId) return Results.Unauthorized();

            var todo = new TodoItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantContext.CurrentTenantId,
                UserId = userId,
                Title = req.Title.Trim(),
                DueDate = req.DueDate
            };

            db.TodoItems.Add(todo);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/workspace/todos/{todo.Id}", todo);
        });

        group.MapPatch("/todos/{id:guid}", async (Guid id, ToggleTodoRequest req, AppDbContext db) =>
        {
            var todo = await db.TodoItems.FirstOrDefaultAsync(t => t.Id == id);
            if (todo is null) return Results.NotFound();

            todo.IsCompleted = req.IsCompleted;
            await db.SaveChangesAsync();
            return Results.Ok(todo);
        });

        group.MapDelete("/todos/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var todo = await db.TodoItems.FirstOrDefaultAsync(t => t.Id == id);
            if (todo is null) return Results.NotFound();

            todo.IsDeleted = true;
            todo.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Dashboard KPI Summary
        group.MapGet("/dashboard", async (AppDbContext db, ITenantContext tenantContext) =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var projects = await db.Projects.ToListAsync();
            var tasks = await db.ProjectTasks.ToListAsync();
            var clientsCount = await db.Clients.CountAsync();
            var teamCount = await db.TenantMemberships.CountAsync();

            var activeProjects = projects.Count(p => p.Status is "Open" or "InProgress");
            var openTasks = tasks.Count(t => t.Status != "Done");
            var overdueTasks = tasks.Count(t => t.Status != "Done" && t.Deadline.HasValue && t.Deadline.Value < today);

            var projectProgress = projects.Take(5).Select(p =>
            {
                var pTasks = tasks.Where(t => t.ProjectId == p.Id).ToList();
                var totalPts = pTasks.Sum(t => Math.Max(1, t.Points));
                var donePts = pTasks.Where(t => t.Status == "Done").Sum(t => Math.Max(1, t.Points));
                return new
                {
                    p.Id,
                    p.Title,
                    p.Deadline,
                    p.Status,
                    progress = totalPts == 0 ? 0 : (int)Math.Round(donePts * 100.0 / totalPts)
                };
            });

            var myTasks = tasks
                .Where(t => t.Status != "Done")
                .OrderByDescending(t => t.CreatedAt)
                .Take(6)
                .Select(t => new { t.Id, t.ProjectId, t.Title, t.Priority, t.Status, t.Deadline });

            var statusBreakdown = new
            {
                toDo = tasks.Count(t => t.Status == "ToDo"),
                inProgress = tasks.Count(t => t.Status == "InProgress"),
                review = tasks.Count(t => t.Status == "Review"),
                done = tasks.Count(t => t.Status == "Done"),
                total = tasks.Count
            };

            var recentActivity = await db.AuditLogs
                .Where(a => a.TenantId == tenantContext.CurrentTenantId)
                .OrderByDescending(a => a.CreatedAt)
                .Take(6)
                .Select(a => new { a.Id, a.Action, a.EntityName, a.EntityId, a.CreatedAt })
                .ToListAsync();

            return Results.Ok(new
            {
                kpis = new { activeProjects, openTasks, overdueTasks, clientsCount, teamCount },
                projectProgress,
                myTasks,
                statusBreakdown,
                recentActivity
            });
        });

        // Audit Logs
        group.MapGet("/audit-logs", async (AppDbContext db, ITenantContext tenantContext) =>
        {
            var logs = await db.AuditLogs
                .Where(a => a.TenantId == tenantContext.CurrentTenantId)
                .OrderByDescending(a => a.CreatedAt)
                .Take(50)
                .Select(a => new { a.Id, a.Action, a.EntityName, a.EntityId, a.ActorUserId, a.CreatedAt })
                .ToListAsync();
            return Results.Ok(logs);
        });

        // Tenant Settings
        app.MapPut("/api/v1/tenant/settings", async (UpdateTenantSettingsRequest req, AppDbContext db, ITenantContext tenantContext) =>
        {
            var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantContext.CurrentTenantId);
            if (tenant is null) return Results.NotFound();

            if (!string.IsNullOrWhiteSpace(req.CompanyName))
                tenant.CompanyName = req.CompanyName.Trim();

            await db.SaveChangesAsync();
            return Results.Ok(new { tenant.Id, tenant.CompanyName, tenant.Slug });
        }).RequireAuthorization();
    }
}
