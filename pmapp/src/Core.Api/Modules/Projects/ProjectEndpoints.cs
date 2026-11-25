using Core.Api.Domain;
using Core.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using PMApp.Plugin.Abstractions;

namespace Core.Api.Modules.Projects;

public static class ProjectEndpoints
{
    public sealed record ProjectRequest(string Title, string? Description, Guid? ClientId, DateOnly? StartDate, DateOnly? Deadline, string? Status, string? Labels);
    public sealed record MilestoneRequest(string Title, DateOnly? DueDate, int SortOrder);
    public sealed record TaskRequest(string Title, string? Description, Guid? MilestoneId, int Points, string? Status, string? Priority, Guid? AssignedToUserId, DateOnly? Deadline);
    public sealed record UpdateTaskStatusRequest(string Status);
    public sealed record BatchCreateTasksRequest(List<TaskRequest> Tasks);
    public sealed record BatchUpdateTasksRequest(List<Guid> TaskIds, string? Status, string? Priority, Guid? MilestoneId, Guid? AssignedToUserId);

    public static void MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        var projects = app.MapGroup("/api/v1/projects").RequireAuthorization();

        projects.MapGet("/", async (string? status, AppDbContext db) =>
        {
            var query = db.Projects.AsQueryable();
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(p => p.Status == status);

            var list = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
            var clients = await db.Clients.ToDictionaryAsync(c => c.Id, c => c.CompanyName);
            var tasks = await db.ProjectTasks.Select(t => new { t.ProjectId, t.Points, t.Status }).ToListAsync();

            var result = list.Select(p =>
            {
                var pTasks = tasks.Where(t => t.ProjectId == p.Id).ToList();
                var totalPoints = pTasks.Sum(t => Math.Max(1, t.Points));
                var donePoints = pTasks.Where(t => t.Status == "Done").Sum(t => Math.Max(1, t.Points));
                var progress = totalPoints == 0 ? 0 : (int)Math.Round(donePoints * 100.0 / totalPoints);

                return new
                {
                    p.Id,
                    p.Title,
                    p.Description,
                    p.ClientId,
                    clientName = p.ClientId.HasValue && clients.TryGetValue(p.ClientId.Value, out var cn) ? cn : null,
                    p.StartDate,
                    p.Deadline,
                    p.Status,
                    p.Labels,
                    progress,
                    totalTasks = pTasks.Count,
                    doneTasks = pTasks.Count(t => t.Status == "Done"),
                    p.CreatedAt
                };
            });

            return Results.Ok(result);
        });

        projects.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var p = await db.Projects.FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound();

            var clientName = p.ClientId.HasValue
                ? await db.Clients.Where(c => c.Id == p.ClientId.Value).Select(c => c.CompanyName).FirstOrDefaultAsync()
                : null;

            var milestones = await db.Milestones.Where(m => m.ProjectId == id).OrderBy(m => m.SortOrder).ToListAsync();
            var tasks = await db.ProjectTasks.Where(t => t.ProjectId == id).OrderBy(t => t.SortOrder).ThenByDescending(t => t.CreatedAt).ToListAsync();

            var totalPoints = tasks.Sum(t => Math.Max(1, t.Points));
            var donePoints = tasks.Where(t => t.Status == "Done").Sum(t => Math.Max(1, t.Points));
            var progress = totalPoints == 0 ? 0 : (int)Math.Round(donePoints * 100.0 / totalPoints);

            var milestoneDtos = milestones.Select(m =>
            {
                var mTasks = tasks.Where(t => t.MilestoneId == m.Id).ToList();
                var mTotal = mTasks.Sum(t => Math.Max(1, t.Points));
                var mDone = mTasks.Where(t => t.Status == "Done").Sum(t => Math.Max(1, t.Points));
                return new
                {
                    m.Id,
                    m.Title,
                    m.DueDate,
                    m.SortOrder,
                    progress = mTotal == 0 ? 0 : (int)Math.Round(mDone * 100.0 / mTotal),
                    totalTasks = mTasks.Count,
                    doneTasks = mTasks.Count(t => t.Status == "Done")
                };
            });

            return Results.Ok(new
            {
                p.Id,
                p.Title,
                p.Description,
                p.ClientId,
                clientName,
                p.StartDate,
                p.Deadline,
                p.Status,
                p.Labels,
                progress,
                milestones = milestoneDtos,
                tasks
            });
        });

        projects.MapPost("/", async (ProjectRequest req, AppDbContext db, ITenantContext tenantContext) =>
        {
            if (string.IsNullOrWhiteSpace(req.Title))
                return Results.Problem("Project title is required.", statusCode: StatusCodes.Status400BadRequest);

            var project = new Project
            {
                Id = Guid.NewGuid(),
                TenantId = tenantContext.CurrentTenantId,
                ClientId = req.ClientId,
                Title = req.Title.Trim(),
                Description = req.Description?.Trim() ?? string.Empty,
                StartDate = req.StartDate,
                Deadline = req.Deadline,
                Status = string.IsNullOrWhiteSpace(req.Status) ? "InProgress" : req.Status.Trim(),
                Labels = req.Labels?.Trim() ?? string.Empty
            };

            db.Projects.Add(project);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/projects/{project.Id}", project);
        });

        projects.MapPut("/{id:guid}", async (Guid id, ProjectRequest req, AppDbContext db) =>
        {
            var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id);
            if (project is null) return Results.NotFound();

            project.Title = req.Title.Trim();
            project.Description = req.Description?.Trim() ?? project.Description;
            project.ClientId = req.ClientId;
            project.StartDate = req.StartDate;
            project.Deadline = req.Deadline;
            if (!string.IsNullOrWhiteSpace(req.Status)) project.Status = req.Status.Trim();
            if (req.Labels is not null) project.Labels = req.Labels.Trim();

            await db.SaveChangesAsync();
            return Results.Ok(project);
        });

        projects.MapDelete("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == id);
            if (project is null) return Results.NotFound();

            project.IsDeleted = true;
            project.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        projects.MapPost("/{id:guid}/restore", async (Guid id, AppDbContext db, ITenantContext tenantContext) =>
        {
            var project = await db.Projects
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantContext.CurrentTenantId && p.IsDeleted);
            if (project is null) return Results.NotFound();

            project.IsDeleted = false;
            project.DeletedAt = null;
            await db.SaveChangesAsync();
            return Results.Ok(project);
        });

        // Milestones
        projects.MapPost("/{projectId:guid}/milestones", async (Guid projectId, MilestoneRequest req, AppDbContext db, ITenantContext tenantContext) =>
        {
            if (!await db.Projects.AnyAsync(p => p.Id == projectId)) return Results.NotFound();

            var milestone = new Milestone
            {
                Id = Guid.NewGuid(),
                TenantId = tenantContext.CurrentTenantId,
                ProjectId = projectId,
                Title = req.Title.Trim(),
                DueDate = req.DueDate,
                SortOrder = req.SortOrder
            };

            db.Milestones.Add(milestone);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/projects/{projectId}/milestones/{milestone.Id}", milestone);
        });

        projects.MapDelete("/{projectId:guid}/milestones/{milestoneId:guid}", async (Guid projectId, Guid milestoneId, AppDbContext db) =>
        {
            var m = await db.Milestones.FirstOrDefaultAsync(x => x.Id == milestoneId && x.ProjectId == projectId);
            if (m is null) return Results.NotFound();

            m.IsDeleted = true;
            m.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Tasks under Project
        projects.MapPost("/{projectId:guid}/tasks", async (Guid projectId, TaskRequest req, AppDbContext db, ITenantContext tenantContext) =>
        {
            if (!await db.Projects.AnyAsync(p => p.Id == projectId)) return Results.NotFound();

            var task = new ProjectTask
            {
                Id = Guid.NewGuid(),
                TenantId = tenantContext.CurrentTenantId,
                ProjectId = projectId,
                MilestoneId = req.MilestoneId,
                Title = req.Title.Trim(),
                Description = req.Description?.Trim() ?? string.Empty,
                Points = Math.Max(1, req.Points),
                Status = string.IsNullOrWhiteSpace(req.Status) ? "ToDo" : req.Status.Trim(),
                Priority = string.IsNullOrWhiteSpace(req.Priority) ? "Medium" : req.Priority.Trim(),
                AssignedToUserId = req.AssignedToUserId,
                Deadline = req.Deadline
            };

            db.ProjectTasks.Add(task);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/tasks/{task.Id}", task);
        });

        projects.MapPost("/{projectId:guid}/tasks/batch-create", async (Guid projectId, BatchCreateTasksRequest req, AppDbContext db, ITenantContext tenantContext) =>
        {
            if (!await db.Projects.AnyAsync(p => p.Id == projectId)) return Results.NotFound();

            var created = req.Tasks.Select(t => new ProjectTask
            {
                Id = Guid.NewGuid(),
                TenantId = tenantContext.CurrentTenantId,
                ProjectId = projectId,
                MilestoneId = t.MilestoneId,
                Title = t.Title.Trim(),
                Description = t.Description?.Trim() ?? string.Empty,
                Points = Math.Max(1, t.Points),
                Status = string.IsNullOrWhiteSpace(t.Status) ? "ToDo" : t.Status.Trim(),
                Priority = string.IsNullOrWhiteSpace(t.Priority) ? "Medium" : t.Priority.Trim(),
                AssignedToUserId = t.AssignedToUserId,
                Deadline = t.Deadline
            }).ToList();

            db.ProjectTasks.AddRange(created);
            await db.SaveChangesAsync();
            return Results.Ok(created);
        });

        // Global Tasks Endpoints
        var tasksGroup = app.MapGroup("/api/v1/tasks").RequireAuthorization();

        tasksGroup.MapGet("/", async (Guid? projectId, string? status, AppDbContext db) =>
        {
            var q = db.ProjectTasks.AsQueryable();
            if (projectId.HasValue) q = q.Where(t => t.ProjectId == projectId.Value);
            if (!string.IsNullOrWhiteSpace(status)) q = q.Where(t => t.Status == status);

            var projectNames = await db.Projects.ToDictionaryAsync(p => p.Id, p => p.Title);
            var list = await q.OrderByDescending(t => t.CreatedAt).ToListAsync();

            return Results.Ok(list.Select(t => new
            {
                t.Id,
                t.ProjectId,
                projectTitle = projectNames.TryGetValue(t.ProjectId, out var pt) ? pt : "",
                t.MilestoneId,
                t.Title,
                t.Description,
                t.Points,
                t.Status,
                t.Priority,
                t.AssignedToUserId,
                t.Deadline,
                t.CreatedAt
            }));
        });

        tasksGroup.MapPatch("/{id:guid}/status", async (Guid id, UpdateTaskStatusRequest req, AppDbContext db) =>
        {
            var task = await db.ProjectTasks.FirstOrDefaultAsync(t => t.Id == id);
            if (task is null) return Results.NotFound();

            task.Status = req.Status.Trim();
            await db.SaveChangesAsync();
            return Results.Ok(task);
        });

        tasksGroup.MapPost("/batch-update", async (BatchUpdateTasksRequest req, AppDbContext db) =>
        {
            var items = await db.ProjectTasks.Where(t => req.TaskIds.Contains(t.Id)).ToListAsync();
            foreach (var t in items)
            {
                if (!string.IsNullOrWhiteSpace(req.Status)) t.Status = req.Status.Trim();
                if (!string.IsNullOrWhiteSpace(req.Priority)) t.Priority = req.Priority.Trim();
                if (req.MilestoneId.HasValue) t.MilestoneId = req.MilestoneId.Value;
                if (req.AssignedToUserId.HasValue) t.AssignedToUserId = req.AssignedToUserId.Value;
            }
            await db.SaveChangesAsync();
            return Results.Ok(new { updatedCount = items.Count });
        });

        tasksGroup.MapDelete("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var task = await db.ProjectTasks.FirstOrDefaultAsync(t => t.Id == id);
            if (task is null) return Results.NotFound();

            task.IsDeleted = true;
            task.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        tasksGroup.MapPost("/{id:guid}/restore", async (Guid id, AppDbContext db, ITenantContext tenantContext) =>
        {
            var task = await db.ProjectTasks
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantContext.CurrentTenantId && t.IsDeleted);
            if (task is null) return Results.NotFound();

            task.IsDeleted = false;
            task.DeletedAt = null;
            await db.SaveChangesAsync();
            return Results.Ok(task);
        });
    }
}
