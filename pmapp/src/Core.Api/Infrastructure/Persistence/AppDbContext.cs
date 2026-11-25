using System.Data.Common;
using System.Linq.Expressions;
using Core.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PMApp.Plugin.Abstractions;

namespace Core.Api.Infrastructure.Persistence;

public sealed class TenantRlsConnectionInterceptor(ITenantContext tenantContext) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.current_tenant_id', @tid, false), set_config('app.is_super_admin', @sa, false);";
        var p = cmd.CreateParameter();
        p.ParameterName = "tid";
        p.Value = tenantContext.CurrentTenantId.ToString();
        cmd.Parameters.Add(p);
        var p2 = cmd.CreateParameter();
        p2.ParameterName = "sa";
        p2.Value = tenantContext.IsSuperAdmin ? "true" : "false";
        cmd.Parameters.Add(p2);
        cmd.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.current_tenant_id', @tid, false), set_config('app.is_super_admin', @sa, false);";
        var p = cmd.CreateParameter();
        p.ParameterName = "tid";
        p.Value = tenantContext.CurrentTenantId.ToString();
        cmd.Parameters.Add(p);
        var p2 = cmd.CreateParameter();
        p2.ParameterName = "sa";
        p2.Value = tenantContext.IsSuperAdmin ? "true" : "false";
        cmd.Parameters.Add(p2);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
    : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PluginCatalogItem> PluginCatalog => Set<PluginCatalogItem>();
    public DbSet<TenantPluginEntitlement> TenantPluginEntitlements => Set<TenantPluginEntitlement>();
    public DbSet<LicenseNumber> LicenseNumbers => Set<LicenseNumber>();
    public DbSet<PlatformInvoice> PlatformInvoices => Set<PlatformInvoice>();
    public DbSet<WorkspaceNote> WorkspaceNotes => Set<WorkspaceNote>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientContact> ClientContacts => Set<ClientContact>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(b =>
        {
            b.ToTable("tenants");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Slug).IsUnique();
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(100);
            b.Property(x => x.CompanyName).HasColumnName("company_name").HasMaxLength(200);
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            b.Property(x => x.MaxUsersQuota).HasColumnName("max_users_quota");
            b.Property(x => x.MaxStorageBytes).HasColumnName("max_storage_bytes");
            b.Property(x => x.UsedStorageBytes).HasColumnName("used_storage_bytes");
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("users");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Email).IsUnique();
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.Email).HasColumnName("email").HasMaxLength(255);
            b.Property(x => x.PasswordHash).HasColumnName("password_hash");
            b.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(200);
            b.Property(x => x.IsSuperAdmin).HasColumnName("is_super_admin");
            b.Property(x => x.LastActiveTenantId).HasColumnName("last_active_tenant_id");
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<TenantMembership>(b =>
        {
            b.ToTable("tenant_memberships");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.TenantId).HasColumnName("tenant_id");
            b.Property(x => x.UserId).HasColumnName("user_id");
            b.Property(x => x.RoleId).HasColumnName("role_id");
            b.Property(x => x.IsOwner).HasColumnName("is_owner");
            b.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            b.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            b.Property(x => x.JoinedAt).HasColumnName("joined_at");
        });

        modelBuilder.Entity<Role>(b =>
        {
            b.ToTable("roles");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.TenantId).HasColumnName("tenant_id");
            b.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
            b.Property(x => x.IsSystem).HasColumnName("is_system");
            b.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            b.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        });

        modelBuilder.Entity<RolePermission>(b =>
        {
            b.ToTable("role_permissions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.TenantId).HasColumnName("tenant_id");
            b.Property(x => x.RoleId).HasColumnName("role_id");
            b.Property(x => x.PermissionKey).HasColumnName("permission_key").HasMaxLength(150);
            b.Property(x => x.PluginId).HasColumnName("plugin_id").HasMaxLength(100);
            b.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            b.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        });

        modelBuilder.Entity<AuditLog>(b =>
        {
            b.ToTable("audit_logs");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.CreatedAt });
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.TenantId).HasColumnName("tenant_id");
            b.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
            b.Property(x => x.Action).HasColumnName("action").HasMaxLength(50);
            b.Property(x => x.EntityName).HasColumnName("entity_name").HasMaxLength(100);
            b.Property(x => x.EntityId).HasColumnName("entity_id").HasMaxLength(100);
            b.Property(x => x.OldValuesJson).HasColumnName("old_values_json");
            b.Property(x => x.NewValuesJson).HasColumnName("new_values_json");
            b.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
            b.Property(x => x.UserAgent).HasColumnName("user_agent").HasMaxLength(512);
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<PluginCatalogItem>(b =>
        {
            b.ToTable("plugin_catalog");
            b.HasKey(x => x.PluginId);
            b.Property(x => x.PluginId).HasColumnName("plugin_id").HasMaxLength(100);
            b.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
            b.Property(x => x.Description).HasColumnName("description");
            b.Property(x => x.Version).HasColumnName("version").HasMaxLength(50);
            b.Property(x => x.IsGloballyEnabled).HasColumnName("is_globally_enabled");
            b.Property(x => x.DefaultPrice).HasColumnName("default_price");
        });

        modelBuilder.Entity<TenantPluginEntitlement>(b =>
        {
            b.ToTable("tenant_plugin_entitlements");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.TenantId, x.PluginId }).IsUnique();
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.TenantId).HasColumnName("tenant_id");
            b.Property(x => x.PluginId).HasColumnName("plugin_id").HasMaxLength(100);
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            b.Property(x => x.IsEnabledByTenant).HasColumnName("is_enabled_by_tenant");
            b.Property(x => x.LicenseNumberId).HasColumnName("license_number_id");
            b.Property(x => x.LatestInvoiceId).HasColumnName("latest_invoice_id");
            b.Property(x => x.RequestedByUserId).HasColumnName("requested_by_user_id");
            b.Property(x => x.RequestedAt).HasColumnName("requested_at");
            b.Property(x => x.ActivatedAt).HasColumnName("activated_at");
            b.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            b.Property(x => x.RejectionReason).HasColumnName("rejection_reason");
            b.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            b.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        });

        modelBuilder.Entity<LicenseNumber>(b =>
        {
            b.ToTable("license_numbers");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.LicenseKey).IsUnique();
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.LicenseKey).HasColumnName("license_key").HasMaxLength(100);
            b.Property(x => x.PluginId).HasColumnName("plugin_id").HasMaxLength(100);
            b.Property(x => x.DurationType).HasColumnName("duration_type").HasMaxLength(32);
            b.Property(x => x.InvoiceAmount).HasColumnName("invoice_amount");
            b.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(10);
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            b.Property(x => x.RedeemedByTenantId).HasColumnName("redeemed_by_tenant_id");
            b.Property(x => x.RedeemedAt).HasColumnName("redeemed_at");
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<PlatformInvoice>(b =>
        {
            b.ToTable("platform_invoices");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.InvoiceNumber).IsUnique();
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.InvoiceNumber).HasColumnName("invoice_number").HasMaxLength(50);
            b.Property(x => x.TenantId).HasColumnName("tenant_id");
            b.Property(x => x.LicenseNumberId).HasColumnName("license_number_id");
            b.Property(x => x.PluginId).HasColumnName("plugin_id").HasMaxLength(100);
            b.Property(x => x.Amount).HasColumnName("amount");
            b.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(10);
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            b.Property(x => x.PaidAt).HasColumnName("paid_at");
        });

        modelBuilder.Entity<WorkspaceNote>(b =>
        {
            b.ToTable("workspace_notes");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.TenantId);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.TenantId).HasColumnName("tenant_id");
            b.Property(x => x.AuthorUserId).HasColumnName("author_user_id");
            b.Property(x => x.Title).HasColumnName("title").HasMaxLength(250);
            b.Property(x => x.Content).HasColumnName("content");
            b.Property(x => x.IsSticky).HasColumnName("is_sticky");
            b.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            b.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<Client>(b =>
        {
            b.ToTable("clients");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.TenantId).HasColumnName("tenant_id");
            b.Property(x => x.CompanyName).HasColumnName("company_name").HasMaxLength(200);
            b.Property(x => x.Email).HasColumnName("email").HasMaxLength(255);
            b.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(50);
            b.Property(x => x.Address).HasColumnName("address");
            b.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(10);
            b.Property(x => x.Website).HasColumnName("website").HasMaxLength(255);
            b.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            b.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<ClientContact>(b =>
        {
            b.ToTable("client_contacts");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.TenantId).HasColumnName("tenant_id");
            b.Property(x => x.ClientId).HasColumnName("client_id");
            b.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(200);
            b.Property(x => x.Email).HasColumnName("email").HasMaxLength(255);
            b.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(50);
            b.Property(x => x.JobTitle).HasColumnName("job_title").HasMaxLength(100);
            b.Property(x => x.IsPrimary).HasColumnName("is_primary");
            b.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            b.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        });

        modelBuilder.Entity<Project>(b =>
        {
            b.ToTable("projects");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.TenantId).HasColumnName("tenant_id");
            b.Property(x => x.ClientId).HasColumnName("client_id");
            b.Property(x => x.Title).HasColumnName("title").HasMaxLength(250);
            b.Property(x => x.Description).HasColumnName("description");
            b.Property(x => x.StartDate).HasColumnName("start_date");
            b.Property(x => x.Deadline).HasColumnName("deadline");
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            b.Property(x => x.Labels).HasColumnName("labels").HasMaxLength(500);
            b.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            b.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<Milestone>(b =>
        {
            b.ToTable("milestones");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.TenantId).HasColumnName("tenant_id");
            b.Property(x => x.ProjectId).HasColumnName("project_id");
            b.Property(x => x.Title).HasColumnName("title").HasMaxLength(200);
            b.Property(x => x.DueDate).HasColumnName("due_date");
            b.Property(x => x.SortOrder).HasColumnName("sort_order");
            b.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            b.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        });

        modelBuilder.Entity<ProjectTask>(b =>
        {
            b.ToTable("project_tasks");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.TenantId).HasColumnName("tenant_id");
            b.Property(x => x.ProjectId).HasColumnName("project_id");
            b.Property(x => x.MilestoneId).HasColumnName("milestone_id");
            b.Property(x => x.Title).HasColumnName("title").HasMaxLength(250);
            b.Property(x => x.Description).HasColumnName("description");
            b.Property(x => x.Points).HasColumnName("points");
            b.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            b.Property(x => x.Priority).HasColumnName("priority").HasMaxLength(32);
            b.Property(x => x.AssignedToUserId).HasColumnName("assigned_to_user_id");
            b.Property(x => x.Deadline).HasColumnName("deadline");
            b.Property(x => x.SortOrder).HasColumnName("sort_order");
            b.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            b.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<TodoItem>(b =>
        {
            b.ToTable("todo_items");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.TenantId).HasColumnName("tenant_id");
            b.Property(x => x.UserId).HasColumnName("user_id");
            b.Property(x => x.Title).HasColumnName("title").HasMaxLength(250);
            b.Property(x => x.IsCompleted).HasColumnName("is_completed");
            b.Property(x => x.DueDate).HasColumnName("due_date");
            b.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            b.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            b.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // Apply EF Core Global Query Filter to every ITenantEntity
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                var param = Expression.Parameter(entityType.ClrType, "e");
                var tenantProp = Expression.Property(param, nameof(ITenantEntity.TenantId));
                var isDeletedProp = Expression.Property(param, nameof(ITenantEntity.IsDeleted));
                var currentTenantExpr = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
                var filter = Expression.Lambda(
                    Expression.AndAlso(
                        Expression.Equal(tenantProp, currentTenantExpr),
                        Expression.Not(isDeletedProp)),
                    param);
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
            }
        }
    }

    public Guid CurrentTenantId => tenantContext.CurrentTenantId;
}
