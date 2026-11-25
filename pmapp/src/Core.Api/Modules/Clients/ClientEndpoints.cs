using Core.Api.Domain;
using Core.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using PMApp.Plugin.Abstractions;

namespace Core.Api.Modules.Clients;

public static class ClientEndpoints
{
    public sealed record ClientRequest(string CompanyName, string? Email, string? Phone, string? Address, string? Currency, string? Website);
    public sealed record ContactRequest(string FullName, string Email, string? Phone, string? JobTitle, bool IsPrimary);

    public static void MapClientEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/clients").RequireAuthorization();

        group.MapGet("/", async (string? search, AppDbContext db) =>
        {
            var query = db.Clients.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(c => c.CompanyName.ToLower().Contains(term) || (c.Email != null && c.Email.ToLower().Contains(term)));
            }

            var items = await query
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new { c.Id, c.CompanyName, c.Email, c.Phone, c.Address, c.Currency, c.Website, c.CreatedAt })
                .ToListAsync();
            return Results.Ok(items);
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == id);
            if (client is null) return Results.NotFound();

            var contacts = await db.ClientContacts
                .Where(c => c.ClientId == id)
                .OrderByDescending(c => c.IsPrimary)
                .Select(c => new { c.Id, c.FullName, c.Email, c.Phone, c.JobTitle, c.IsPrimary })
                .ToListAsync();

            var projects = await db.Projects
                .Where(p => p.ClientId == id)
                .Select(p => new { p.Id, p.Title, p.Status, p.Deadline })
                .ToListAsync();

            return Results.Ok(new
            {
                client.Id,
                client.CompanyName,
                client.Email,
                client.Phone,
                client.Address,
                client.Currency,
                client.Website,
                client.CreatedAt,
                contacts,
                projects
            });
        });

        group.MapPost("/", async (ClientRequest req, AppDbContext db, ITenantContext tenantContext) =>
        {
            if (string.IsNullOrWhiteSpace(req.CompanyName))
                return Results.Problem("Company name is required.", statusCode: StatusCodes.Status400BadRequest);

            var client = new Client
            {
                Id = Guid.NewGuid(),
                TenantId = tenantContext.CurrentTenantId,
                CompanyName = req.CompanyName.Trim(),
                Email = req.Email?.Trim(),
                Phone = req.Phone?.Trim(),
                Address = req.Address?.Trim(),
                Currency = string.IsNullOrWhiteSpace(req.Currency) ? "IDR" : req.Currency.Trim().ToUpperInvariant(),
                Website = req.Website?.Trim()
            };

            db.Clients.Add(client);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/clients/{client.Id}", client);
        });

        group.MapPut("/{id:guid}", async (Guid id, ClientRequest req, AppDbContext db) =>
        {
            var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == id);
            if (client is null) return Results.NotFound();

            client.CompanyName = req.CompanyName.Trim();
            client.Email = req.Email?.Trim();
            client.Phone = req.Phone?.Trim();
            client.Address = req.Address?.Trim();
            client.Currency = string.IsNullOrWhiteSpace(req.Currency) ? client.Currency : req.Currency.Trim().ToUpperInvariant();
            client.Website = req.Website?.Trim();

            await db.SaveChangesAsync();
            return Results.Ok(client);
        });

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == id);
            if (client is null) return Results.NotFound();

            client.IsDeleted = true;
            client.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // 10-Second Undo Delete restore endpoint
        group.MapPost("/{id:guid}/restore", async (Guid id, AppDbContext db, ITenantContext tenantContext) =>
        {
            var client = await db.Clients
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenantContext.CurrentTenantId && c.IsDeleted);
            if (client is null) return Results.NotFound();

            client.IsDeleted = false;
            client.DeletedAt = null;
            await db.SaveChangesAsync();
            return Results.Ok(client);
        });

        group.MapPost("/{id:guid}/contacts", async (Guid id, ContactRequest req, AppDbContext db, ITenantContext tenantContext) =>
        {
            if (!await db.Clients.AnyAsync(c => c.Id == id)) return Results.NotFound();

            var contact = new ClientContact
            {
                Id = Guid.NewGuid(),
                TenantId = tenantContext.CurrentTenantId,
                ClientId = id,
                FullName = req.FullName.Trim(),
                Email = req.Email.Trim(),
                Phone = req.Phone?.Trim(),
                JobTitle = req.JobTitle?.Trim(),
                IsPrimary = req.IsPrimary
            };

            db.ClientContacts.Add(contact);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/clients/{id}/contacts/{contact.Id}", contact);
        });

        group.MapDelete("/{clientId:guid}/contacts/{contactId:guid}", async (Guid clientId, Guid contactId, AppDbContext db) =>
        {
            var contact = await db.ClientContacts.FirstOrDefaultAsync(c => c.Id == contactId && c.ClientId == clientId);
            if (contact is null) return Results.NotFound();

            contact.IsDeleted = true;
            contact.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
