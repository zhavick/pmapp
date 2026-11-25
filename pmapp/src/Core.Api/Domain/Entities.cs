using PMApp.Plugin.Abstractions;

namespace Core.Api.Domain;

public sealed class Tenant
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Status { get; set; } = "Active"; // Active | Suspended
    public int MaxUsersQuota { get; set; } = 10; // Free Core default
    public long MaxStorageBytes { get; set; } = 2L * 1024 * 1024 * 1024; // 2 GB
    public long UsedStorageBytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsSuperAdmin { get; set; }
    public Guid? LastActiveTenantId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TenantMembership : ITenantEntity, IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public bool IsOwner { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }
    public Role? Role { get; set; }
}

public sealed class Role : ITenantEntity, IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();
}

public sealed class RolePermission : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RoleId { get; set; }
    public string PermissionKey { get; set; } = string.Empty;
    public string? PluginId { get; set; } // Null for Core permissions
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public sealed class AuditLog
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty; // Create | Update | Delete | Login | RegisterTenant
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PluginCatalogItem
{
    public string PluginId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public bool IsGloballyEnabled { get; set; } = true;
    public decimal DefaultPrice { get; set; }
}

public sealed class TenantPluginEntitlement : ITenantEntity, IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string PluginId { get; set; } = string.Empty;
    public string Status { get; set; } = "Disabled"; // Disabled | PendingApproval | Active | Rejected | Expired
    public bool IsEnabledByTenant { get; set; }
    public Guid? LicenseNumberId { get; set; }
    public Guid? LatestInvoiceId { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public DateTimeOffset? RequestedAt { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? RejectionReason { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public sealed class LicenseNumber
{
    public Guid Id { get; set; }
    public string LicenseKey { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string DurationType { get; set; } = "OneYear"; // OneMonth | OneYear | Lifetime
    public decimal InvoiceAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "Available"; // Available | Redeemed | Revoked
    public Guid? RedeemedByTenantId { get; set; }
    public DateTimeOffset? RedeemedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PlatformInvoice
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public Guid LicenseNumberId { get; set; }
    public string PluginId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "Paid";
    public DateTimeOffset PaidAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Minimal Core entity in Phase 1 used to verify tenant isolation + audit trail end-to-end.
/// </summary>
public sealed class WorkspaceNote : ITenantEntity, IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AuthorUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsSticky { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}


public sealed class Client : ITenantEntity, IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string Currency { get; set; } = "IDR";
    public string? Website { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ClientContact : ITenantEntity, IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ClientId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public sealed class Project : ITenantEntity, IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? ClientId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateOnly? StartDate { get; set; }
    public DateOnly? Deadline { get; set; }
    public string Status { get; set; } = "InProgress"; // Open | InProgress | Completed | OnHold | Canceled
    public string Labels { get; set; } = string.Empty; // comma-separated
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Milestone : ITenantEntity, IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateOnly? DueDate { get; set; }
    public int SortOrder { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public sealed class ProjectTask : ITenantEntity, IAuditableEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? MilestoneId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Points { get; set; } = 1;
    public string Status { get; set; } = "ToDo"; // ToDo | InProgress | Review | Done
    public string Priority { get; set; } = "Medium"; // Low | Medium | High | Urgent
    public Guid? AssignedToUserId { get; set; }
    public DateOnly? Deadline { get; set; }
    public int SortOrder { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class TodoItem : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateOnly? DueDate { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
