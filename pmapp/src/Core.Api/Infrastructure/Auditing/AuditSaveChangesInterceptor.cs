using System.Text.Json;
using Core.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PMApp.Plugin.Abstractions;

namespace Core.Api.Infrastructure.Auditing;

public sealed class AuditSaveChangesInterceptor(ITenantContext tenantContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AppendAuditLogs(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AppendAuditLogs(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AppendAuditLogs(DbContext? context)
    {
        if (context is null) return;

        var entries = context.ChangeTracker.Entries<IAuditableEntity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (entries.Count == 0) return;

        var auditLogs = new List<AuditLog>(entries.Count);

        foreach (var entry in entries)
        {
            var action = entry.State switch
            {
                EntityState.Added => "Create",
                EntityState.Modified => "Update",
                EntityState.Deleted => "Delete",
                _ => "Unknown"
            };

            Guid? entityTenantId = entry.Entity is ITenantEntity te ? te.TenantId : (tenantContext.CurrentTenantId == Guid.Empty ? null : tenantContext.CurrentTenantId);

            auditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = entityTenantId,
                ActorUserId = tenantContext.CurrentUserId,
                Action = action,
                EntityName = entry.Entity.GetType().Name,
                EntityId = entry.Entity.Id.ToString(),
                OldValuesJson = entry.State == EntityState.Added ? null : SerializeProps(entry.Properties, original: true),
                NewValuesJson = entry.State == EntityState.Deleted ? null : SerializeProps(entry.Properties, original: false),
                IpAddress = tenantContext.IpAddress,
                UserAgent = tenantContext.UserAgent,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        context.Set<AuditLog>().AddRange(auditLogs);
    }

    private static string SerializeProps(IEnumerable<PropertyEntry> properties, bool original)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var p in properties)
        {
            if (p.Metadata.Name.Contains("Password", StringComparison.OrdinalIgnoreCase)) continue;
            dict[p.Metadata.Name] = original ? p.OriginalValue : p.CurrentValue;
        }
        return JsonSerializer.Serialize(dict);
    }
}
