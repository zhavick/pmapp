using Npgsql;

namespace Core.Api.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(string adminConnectionString, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(adminConnectionString);
        await conn.OpenAsync(ct);

        const string sql = """
            DO $$ BEGIN
                IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'pmapp_app') THEN
                    CREATE ROLE pmapp_app LOGIN PASSWORD 'pmapp_app_password' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
                END IF;
            END $$;

            CREATE TABLE IF NOT EXISTS tenants (
                id uuid PRIMARY KEY,
                slug varchar(100) NOT NULL UNIQUE,
                company_name varchar(200) NOT NULL,
                status varchar(32) NOT NULL DEFAULT 'Active',
                max_users_quota int NOT NULL DEFAULT 10,
                max_storage_bytes bigint NOT NULL DEFAULT 2147483648,
                used_storage_bytes bigint NOT NULL DEFAULT 0,
                created_at timestamptz NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS users (
                id uuid PRIMARY KEY,
                email varchar(255) NOT NULL UNIQUE,
                password_hash text NOT NULL,
                full_name varchar(200) NOT NULL,
                is_super_admin boolean NOT NULL DEFAULT false,
                last_active_tenant_id uuid NULL,
                created_at timestamptz NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS roles (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
                name varchar(100) NOT NULL,
                is_system boolean NOT NULL DEFAULT false,
                is_deleted boolean NOT NULL DEFAULT false,
                deleted_at timestamptz NULL
            );

            CREATE TABLE IF NOT EXISTS tenant_memberships (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
                user_id uuid NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                role_id uuid NOT NULL REFERENCES roles(id),
                is_owner boolean NOT NULL DEFAULT false,
                is_deleted boolean NOT NULL DEFAULT false,
                deleted_at timestamptz NULL,
                joined_at timestamptz NOT NULL DEFAULT now(),
                UNIQUE(tenant_id, user_id)
            );

            CREATE TABLE IF NOT EXISTS role_permissions (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
                role_id uuid NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
                permission_key varchar(150) NOT NULL,
                plugin_id varchar(100) NULL,
                is_deleted boolean NOT NULL DEFAULT false,
                deleted_at timestamptz NULL
            );

            CREATE TABLE IF NOT EXISTS audit_logs (
                id uuid PRIMARY KEY,
                tenant_id uuid NULL,
                actor_user_id uuid NULL,
                action varchar(50) NOT NULL,
                entity_name varchar(100) NOT NULL,
                entity_id varchar(100) NOT NULL,
                old_values_json text NULL,
                new_values_json text NULL,
                ip_address varchar(64) NULL,
                user_agent varchar(512) NULL,
                created_at timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_audit_logs_tenant_created ON audit_logs (tenant_id, created_at DESC);

            CREATE TABLE IF NOT EXISTS plugin_catalog (
                plugin_id varchar(100) PRIMARY KEY,
                name varchar(200) NOT NULL,
                description text NOT NULL,
                version varchar(50) NOT NULL DEFAULT '1.0.0',
                is_globally_enabled boolean NOT NULL DEFAULT true,
                default_price numeric(18,2) NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS tenant_plugin_entitlements (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
                plugin_id varchar(100) NOT NULL REFERENCES plugin_catalog(plugin_id),
                status varchar(32) NOT NULL DEFAULT 'Disabled',
                is_enabled_by_tenant boolean NOT NULL DEFAULT false,
                license_number_id uuid NULL,
                latest_invoice_id uuid NULL,
                requested_by_user_id uuid NULL,
                requested_at timestamptz NULL,
                activated_at timestamptz NULL,
                expires_at timestamptz NULL,
                rejection_reason text NULL,
                is_deleted boolean NOT NULL DEFAULT false,
                deleted_at timestamptz NULL,
                UNIQUE(tenant_id, plugin_id)
            );
            ALTER TABLE tenant_plugin_entitlements ADD COLUMN IF NOT EXISTS license_number_id uuid NULL;
            ALTER TABLE tenant_plugin_entitlements ADD COLUMN IF NOT EXISTS latest_invoice_id uuid NULL;
            ALTER TABLE tenant_plugin_entitlements ADD COLUMN IF NOT EXISTS requested_by_user_id uuid NULL;
            ALTER TABLE tenant_plugin_entitlements ADD COLUMN IF NOT EXISTS requested_at timestamptz NULL;
            ALTER TABLE tenant_plugin_entitlements ADD COLUMN IF NOT EXISTS rejection_reason text NULL;

            CREATE TABLE IF NOT EXISTS license_numbers (
                id uuid PRIMARY KEY,
                license_key varchar(100) NOT NULL UNIQUE,
                plugin_id varchar(100) NOT NULL REFERENCES plugin_catalog(plugin_id),
                duration_type varchar(32) NOT NULL DEFAULT 'OneYear',
                invoice_amount numeric(18,2) NOT NULL DEFAULT 0,
                currency varchar(10) NOT NULL DEFAULT 'USD',
                status varchar(32) NOT NULL DEFAULT 'Available',
                redeemed_by_tenant_id uuid NULL,
                redeemed_at timestamptz NULL,
                created_at timestamptz NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS platform_invoices (
                id uuid PRIMARY KEY,
                invoice_number varchar(50) NOT NULL UNIQUE,
                tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
                license_number_id uuid NOT NULL REFERENCES license_numbers(id),
                plugin_id varchar(100) NOT NULL,
                amount numeric(18,2) NOT NULL DEFAULT 0,
                currency varchar(10) NOT NULL DEFAULT 'USD',
                status varchar(32) NOT NULL DEFAULT 'Paid',
                paid_at timestamptz NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS workspace_notes (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
                author_user_id uuid NOT NULL,
                title varchar(250) NOT NULL,
                content text NOT NULL DEFAULT '',
                is_sticky boolean NOT NULL DEFAULT false,
                is_deleted boolean NOT NULL DEFAULT false,
                deleted_at timestamptz NULL,
                created_at timestamptz NOT NULL DEFAULT now(),
                updated_at timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_workspace_notes_tenant ON workspace_notes (tenant_id);

            CREATE TABLE IF NOT EXISTS clients (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
                company_name varchar(200) NOT NULL,
                email varchar(255) NULL,
                phone varchar(50) NULL,
                address text NULL,
                currency varchar(10) NOT NULL DEFAULT 'IDR',
                website varchar(255) NULL,
                is_deleted boolean NOT NULL DEFAULT false,
                deleted_at timestamptz NULL,
                created_at timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_clients_tenant ON clients (tenant_id);

            CREATE TABLE IF NOT EXISTS client_contacts (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
                client_id uuid NOT NULL REFERENCES clients(id) ON DELETE CASCADE,
                full_name varchar(200) NOT NULL,
                email varchar(255) NOT NULL,
                phone varchar(50) NULL,
                job_title varchar(100) NULL,
                is_primary boolean NOT NULL DEFAULT false,
                is_deleted boolean NOT NULL DEFAULT false,
                deleted_at timestamptz NULL
            );

            CREATE TABLE IF NOT EXISTS projects (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
                client_id uuid NULL REFERENCES clients(id) ON DELETE SET NULL,
                title varchar(250) NOT NULL,
                description text NOT NULL DEFAULT '',
                start_date date NULL,
                deadline date NULL,
                status varchar(32) NOT NULL DEFAULT 'InProgress',
                labels varchar(500) NOT NULL DEFAULT '',
                is_deleted boolean NOT NULL DEFAULT false,
                deleted_at timestamptz NULL,
                created_at timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_projects_tenant ON projects (tenant_id);

            CREATE TABLE IF NOT EXISTS milestones (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
                project_id uuid NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
                title varchar(200) NOT NULL,
                due_date date NULL,
                sort_order int NOT NULL DEFAULT 0,
                is_deleted boolean NOT NULL DEFAULT false,
                deleted_at timestamptz NULL
            );

            CREATE TABLE IF NOT EXISTS project_tasks (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
                project_id uuid NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
                milestone_id uuid NULL REFERENCES milestones(id) ON DELETE SET NULL,
                title varchar(250) NOT NULL,
                description text NOT NULL DEFAULT '',
                points int NOT NULL DEFAULT 1,
                status varchar(32) NOT NULL DEFAULT 'ToDo',
                priority varchar(32) NOT NULL DEFAULT 'Medium',
                assigned_to_user_id uuid NULL,
                deadline date NULL,
                sort_order int NOT NULL DEFAULT 0,
                is_deleted boolean NOT NULL DEFAULT false,
                deleted_at timestamptz NULL,
                created_at timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_project_tasks_tenant_project ON project_tasks (tenant_id, project_id);

            CREATE TABLE IF NOT EXISTS todo_items (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
                user_id uuid NOT NULL,
                title varchar(250) NOT NULL,
                is_completed boolean NOT NULL DEFAULT false,
                due_date date NULL,
                is_deleted boolean NOT NULL DEFAULT false,
                deleted_at timestamptz NULL,
                created_at timestamptz NOT NULL DEFAULT now()
            );

            GRANT USAGE ON SCHEMA public TO pmapp_app;
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO pmapp_app;

            -- Enforce PostgreSQL Row-Level Security on tenant-scoped tables
            ALTER TABLE roles ENABLE ROW LEVEL SECURITY;
            ALTER TABLE roles FORCE ROW LEVEL SECURITY;
            DROP POLICY IF EXISTS tenant_isolation_roles ON roles;
            CREATE POLICY tenant_isolation_roles ON roles
                USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);

            ALTER TABLE tenant_memberships ENABLE ROW LEVEL SECURITY;
            ALTER TABLE tenant_memberships FORCE ROW LEVEL SECURITY;
            DROP POLICY IF EXISTS tenant_isolation_memberships ON tenant_memberships;
            CREATE POLICY tenant_isolation_memberships ON tenant_memberships
                USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);

            ALTER TABLE role_permissions ENABLE ROW LEVEL SECURITY;
            ALTER TABLE role_permissions FORCE ROW LEVEL SECURITY;
            DROP POLICY IF EXISTS tenant_isolation_role_permissions ON role_permissions;
            CREATE POLICY tenant_isolation_role_permissions ON role_permissions
                USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);

            ALTER TABLE tenant_plugin_entitlements ENABLE ROW LEVEL SECURITY;
            ALTER TABLE tenant_plugin_entitlements FORCE ROW LEVEL SECURITY;
            DROP POLICY IF EXISTS tenant_isolation_entitlements ON tenant_plugin_entitlements;
            CREATE POLICY tenant_isolation_entitlements ON tenant_plugin_entitlements
                USING (
                    current_setting('app.is_super_admin', true) = 'true'
                    OR tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
                )
                WITH CHECK (
                    current_setting('app.is_super_admin', true) = 'true'
                    OR tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid
                );

            ALTER TABLE workspace_notes ENABLE ROW LEVEL SECURITY;
            ALTER TABLE workspace_notes FORCE ROW LEVEL SECURITY;
            DROP POLICY IF EXISTS tenant_isolation_notes ON workspace_notes;
            CREATE POLICY tenant_isolation_notes ON workspace_notes
                USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);

            ALTER TABLE clients ENABLE ROW LEVEL SECURITY;
            ALTER TABLE clients FORCE ROW LEVEL SECURITY;
            DROP POLICY IF EXISTS tenant_isolation_clients ON clients;
            CREATE POLICY tenant_isolation_clients ON clients
                USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);

            ALTER TABLE client_contacts ENABLE ROW LEVEL SECURITY;
            ALTER TABLE client_contacts FORCE ROW LEVEL SECURITY;
            DROP POLICY IF EXISTS tenant_isolation_client_contacts ON client_contacts;
            CREATE POLICY tenant_isolation_client_contacts ON client_contacts
                USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);

            ALTER TABLE projects ENABLE ROW LEVEL SECURITY;
            ALTER TABLE projects FORCE ROW LEVEL SECURITY;
            DROP POLICY IF EXISTS tenant_isolation_projects ON projects;
            CREATE POLICY tenant_isolation_projects ON projects
                USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);

            ALTER TABLE milestones ENABLE ROW LEVEL SECURITY;
            ALTER TABLE milestones FORCE ROW LEVEL SECURITY;
            DROP POLICY IF EXISTS tenant_isolation_milestones ON milestones;
            CREATE POLICY tenant_isolation_milestones ON milestones
                USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);

            ALTER TABLE project_tasks ENABLE ROW LEVEL SECURITY;
            ALTER TABLE project_tasks FORCE ROW LEVEL SECURITY;
            DROP POLICY IF EXISTS tenant_isolation_project_tasks ON project_tasks;
            CREATE POLICY tenant_isolation_project_tasks ON project_tasks
                USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);

            ALTER TABLE todo_items ENABLE ROW LEVEL SECURITY;
            ALTER TABLE todo_items FORCE ROW LEVEL SECURITY;
            DROP POLICY IF EXISTS tenant_isolation_todo_items ON todo_items;
            CREATE POLICY tenant_isolation_todo_items ON todo_items
                USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
