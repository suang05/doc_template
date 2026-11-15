using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmkDoc.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnablePostgreSqlRowLevelSecurityAndRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'smk_app') THEN
                        CREATE ROLE smk_app
                            LOGIN
                            NOSUPERUSER
                            NOCREATEDB
                            NOCREATEROLE
                            NOBYPASSRLS
                            NOREPLICATION
                            PASSWORD 'smk_app_dev_password_change_me';
                    ELSE
                        ALTER ROLE smk_app
                            LOGIN
                            NOSUPERUSER
                            NOCREATEDB
                            NOCREATEROLE
                            NOBYPASSRLS
                            NOREPLICATION;
                    END IF;
                END
                $$;

                GRANT USAGE ON SCHEMA public TO smk_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO smk_app;
                GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA public TO smk_app;
                ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO smk_app;
                ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO smk_app;

                CREATE OR REPLACE FUNCTION public.smk_current_project_id()
                RETURNS uuid
                LANGUAGE plpgsql
                STABLE
                SET search_path = pg_catalog, public
                AS $$
                DECLARE
                    v_raw text := NULLIF(current_setting('app.current_project_id', true), '');
                BEGIN
                    IF v_raw IS NULL THEN
                        RETURN NULL;
                    END IF;
                    RETURN v_raw::uuid;
                EXCEPTION WHEN invalid_text_representation THEN
                    RETURN NULL;
                END;
                $$;

                CREATE OR REPLACE FUNCTION public.smk_is_control_plane()
                RETURNS boolean
                LANGUAGE sql
                STABLE
                SET search_path = pg_catalog, public
                AS $$
                SELECT COALESCE(NULLIF(current_setting('app.control_plane_context', true), ''), 'false') = 'true';
                $$;

                REVOKE ALL ON FUNCTION public.smk_current_project_id() FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION public.smk_current_project_id() TO smk_app;

                REVOKE ALL ON FUNCTION public.smk_is_control_plane() FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION public.smk_is_control_plane() TO smk_app;
                """);

            string[] strictTenantTables =
            [
                "templates",
                "data_connections",
                "datasets",
                "documents",
                "generation_logs",
                "document_jobs"
            ];

            foreach (var table in strictTenantTables)
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE public.{table} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE public.{table} FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS {table}_tenant_isolation ON public.{table};
                    CREATE POLICY {table}_tenant_isolation ON public.{table}
                        AS PERMISSIVE
                        FOR ALL
                        TO PUBLIC
                        USING (project_id = public.smk_current_project_id())
                        WITH CHECK (project_id = public.smk_current_project_id());
                    """);
            }

            migrationBuilder.Sql("""
                ALTER TABLE public.template_versions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE public.template_versions FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS template_versions_tenant_isolation ON public.template_versions;
                CREATE POLICY template_versions_tenant_isolation ON public.template_versions
                    AS PERMISSIVE FOR ALL TO PUBLIC
                    USING (EXISTS (SELECT 1 FROM public.templates AS t WHERE t.id = template_id AND t.project_id = public.smk_current_project_id()))
                    WITH CHECK (EXISTS (SELECT 1 FROM public.templates AS t WHERE t.id = template_id AND t.project_id = public.smk_current_project_id()));

                ALTER TABLE public.field_mappings ENABLE ROW LEVEL SECURITY;
                ALTER TABLE public.field_mappings FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS field_mappings_tenant_isolation ON public.field_mappings;
                CREATE POLICY field_mappings_tenant_isolation ON public.field_mappings
                    AS PERMISSIVE FOR ALL TO PUBLIC
                    USING (EXISTS (SELECT 1 FROM public.templates AS t WHERE t.id = template_id AND t.project_id = public.smk_current_project_id()))
                    WITH CHECK (EXISTS (SELECT 1 FROM public.templates AS t WHERE t.id = template_id AND t.project_id = public.smk_current_project_id()));

                ALTER TABLE public.template_datasets ENABLE ROW LEVEL SECURITY;
                ALTER TABLE public.template_datasets FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS template_datasets_tenant_isolation ON public.template_datasets;
                CREATE POLICY template_datasets_tenant_isolation ON public.template_datasets
                    AS PERMISSIVE FOR ALL TO PUBLIC
                    USING (EXISTS (SELECT 1 FROM public.templates AS t WHERE t.id = template_id AND t.project_id = public.smk_current_project_id()))
                    WITH CHECK (EXISTS (SELECT 1 FROM public.templates AS t WHERE t.id = template_id AND t.project_id = public.smk_current_project_id()));

                ALTER TABLE public.document_versions ENABLE ROW LEVEL SECURITY;
                ALTER TABLE public.document_versions FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS document_versions_tenant_isolation ON public.document_versions;
                CREATE POLICY document_versions_tenant_isolation ON public.document_versions
                    AS PERMISSIVE FOR ALL TO PUBLIC
                    USING (EXISTS (SELECT 1 FROM public.documents AS d WHERE d.id = document_id AND d.project_id = public.smk_current_project_id()))
                    WITH CHECK (EXISTS (SELECT 1 FROM public.documents AS d WHERE d.id = document_id AND d.project_id = public.smk_current_project_id()));

                ALTER TABLE public.document_job_items ENABLE ROW LEVEL SECURITY;
                ALTER TABLE public.document_job_items FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS document_job_items_tenant_isolation ON public.document_job_items;
                CREATE POLICY document_job_items_tenant_isolation ON public.document_job_items
                    AS PERMISSIVE FOR ALL TO PUBLIC
                    USING (EXISTS (SELECT 1 FROM public.document_jobs AS j WHERE j.id = document_job_id AND j.project_id = public.smk_current_project_id()))
                    WITH CHECK (EXISTS (SELECT 1 FROM public.document_jobs AS j WHERE j.id = document_job_id AND j.project_id = public.smk_current_project_id()));
                """);

            string[] controlPlaneDualTables =
            [
                "api_keys",
                "project_quotas",
                "user_project_roles"
            ];

            foreach (var table in controlPlaneDualTables)
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE public.{table} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE public.{table} FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS {table}_tenant_or_control_plane ON public.{table};
                    CREATE POLICY {table}_tenant_or_control_plane ON public.{table}
                        AS PERMISSIVE
                        FOR ALL
                        TO PUBLIC
                        USING (
                            (project_id = public.smk_current_project_id())
                            OR (public.smk_current_project_id() IS NULL AND public.smk_is_control_plane())
                        )
                        WITH CHECK (
                            (project_id = public.smk_current_project_id())
                            OR (public.smk_current_project_id() IS NULL AND public.smk_is_control_plane())
                        );
                    """);
            }

            migrationBuilder.Sql("""
                ALTER TABLE public.idempotency_keys ENABLE ROW LEVEL SECURITY;
                ALTER TABLE public.idempotency_keys FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS idempotency_keys_scope_isolation ON public.idempotency_keys;
                CREATE POLICY idempotency_keys_scope_isolation ON public.idempotency_keys
                    AS PERMISSIVE
                    FOR ALL
                    TO PUBLIC
                    USING (
                        (project_id IS NOT NULL AND project_id = public.smk_current_project_id())
                        OR (project_id IS NULL AND public.smk_current_project_id() IS NULL AND public.smk_is_control_plane())
                    )
                    WITH CHECK (
                        (project_id IS NOT NULL AND project_id = public.smk_current_project_id())
                        OR (project_id IS NULL AND public.smk_current_project_id() IS NULL AND public.smk_is_control_plane())
                    );
                """);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION public.fn_resolve_api_key_for_auth(
                    p_key_id text,
                    p_legacy_hash text)
                RETURNS TABLE (
                    id uuid,
                    project_id uuid,
                    caller_app text,
                    scope text,
                    is_active boolean,
                    expires_at timestamp with time zone,
                    secret_hash_bytes bytea,
                    key_hash text
                )
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path = pg_catalog, public
                AS $$
                #variable_conflict use_column
                BEGIN
                    RETURN QUERY
                    SELECT k.id,
                           k.project_id,
                           k.caller_app::text,
                           k.scope::text,
                           k.is_active,
                           k.expires_at,
                           k.secret_hash_bytes,
                           k.key_hash::text
                    FROM public.api_keys AS k
                    WHERE (p_key_id IS NOT NULL AND k.key_id = p_key_id)
                       OR (p_key_id IS NULL AND p_legacy_hash IS NOT NULL AND k.secret_hash_bytes IS NULL AND k.key_hash = p_legacy_hash)
                    LIMIT 1;
                END;
                $$;

                CREATE OR REPLACE FUNCTION public.fn_touch_api_key_last_used(
                    p_id uuid,
                    p_now timestamp with time zone)
                RETURNS void
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path = pg_catalog, public
                AS $$
                #variable_conflict use_column
                BEGIN
                    UPDATE public.api_keys AS k
                    SET last_used_at = p_now
                    WHERE k.id = p_id;
                END;
                $$;

                CREATE OR REPLACE FUNCTION public.fn_resolve_user_project_role_for_auth(
                    p_project_id uuid,
                    p_user_id uuid)
                RETURNS TABLE (
                    project_id uuid,
                    user_id uuid,
                    role text
                )
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path = pg_catalog, public
                AS $$
                #variable_conflict use_column
                BEGIN
                    RETURN QUERY
                    SELECT upr.project_id,
                           upr.user_id,
                           upr.role::text
                    FROM public.user_project_roles AS upr
                    WHERE upr.project_id = p_project_id
                      AND upr.user_id = p_user_id
                    LIMIT 1;
                END;
                $$;

                CREATE OR REPLACE FUNCTION public.fn_list_active_project_ids()
                RETURNS TABLE (project_id uuid)
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path = pg_catalog, public
                AS $$
                #variable_conflict use_column
                BEGIN
                    RETURN QUERY
                    SELECT p.id
                    FROM public.projects AS p
                    WHERE p.is_active = true
                    ORDER BY p.created_at ASC;
                END;
                $$;

                CREATE OR REPLACE FUNCTION public.fn_claim_next_document_job(
                    p_worker_id text,
                    p_lease_seconds integer)
                RETURNS TABLE (
                    id uuid,
                    project_id uuid
                )
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path = pg_catalog, public
                AS $$
                #variable_conflict use_column
                DECLARE
                    v_now timestamp with time zone := clock_timestamp();
                BEGIN
                    RETURN QUERY
                    WITH candidate AS (
                        SELECT j.id
                        FROM public.document_jobs AS j
                        WHERE (j.status IN ('Queued', 'Retrying') AND j.available_at <= v_now)
                           OR (j.status IN ('Running', 'Pending') AND j.locked_until IS NOT NULL AND j.locked_until <= v_now)
                        ORDER BY j.available_at ASC, j.created_at ASC
                        LIMIT 1
                        FOR UPDATE SKIP LOCKED
                    ),
                    claimed AS (
                        UPDATE public.document_jobs AS j
                        SET status = 'Running',
                            attempt = j.attempt + 1,
                            locked_by = p_worker_id,
                            locked_until = v_now + make_interval(secs => GREATEST(10, p_lease_seconds)),
                            started_at = COALESCE(j.started_at, v_now),
                            updated_at = v_now,
                            version = j.version + 1
                        FROM candidate AS c
                        WHERE j.id = c.id
                        RETURNING j.id, j.project_id
                    )
                    SELECT cl.id, cl.project_id
                    FROM claimed AS cl;
                END;
                $$;

                CREATE OR REPLACE FUNCTION public.fn_renew_document_job_lease(
                    p_job_id uuid,
                    p_worker_id text,
                    p_lease_seconds integer)
                RETURNS TABLE (
                    renewed boolean,
                    cancel_requested boolean
                )
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path = pg_catalog, public
                AS $$
                #variable_conflict use_column
                DECLARE
                    v_now timestamp with time zone := clock_timestamp();
                BEGIN
                    RETURN QUERY
                    UPDATE public.document_jobs AS j
                    SET locked_until = v_now + make_interval(secs => GREATEST(10, p_lease_seconds)),
                        updated_at = v_now
                    WHERE j.id = p_job_id
                      AND j.locked_by = p_worker_id
                      AND j.status = 'Running'
                    RETURNING true AS renewed, j.cancel_requested;
                END;
                $$;

                CREATE OR REPLACE FUNCTION public.fn_claim_outbox_messages(
                    p_worker_id text,
                    p_batch_size integer,
                    p_lease_seconds integer)
                RETURNS TABLE (
                    id uuid,
                    project_id uuid,
                    event_type text,
                    payload_json text,
                    occurred_at timestamp with time zone,
                    attempts integer
                )
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path = pg_catalog, public
                AS $$
                #variable_conflict use_column
                DECLARE
                    v_now timestamp with time zone := clock_timestamp();
                BEGIN
                    RETURN QUERY
                    WITH candidates AS (
                        SELECT m.id
                        FROM public.outbox_messages AS m
                        WHERE m.processed_at IS NULL
                          AND m.dead_lettered_at IS NULL
                          AND m.available_at <= v_now
                          AND (m.locked_until IS NULL OR m.locked_until <= v_now)
                        ORDER BY m.available_at ASC, m.occurred_at ASC
                        LIMIT GREATEST(1, LEAST(p_batch_size, 500))
                        FOR UPDATE SKIP LOCKED
                    ),
                    updated AS (
                        UPDATE public.outbox_messages AS m
                        SET locked_by = p_worker_id,
                            locked_until = v_now + make_interval(secs => GREATEST(5, p_lease_seconds)),
                            attempts = m.attempts + 1
                        FROM candidates AS c
                        WHERE m.id = c.id
                        RETURNING m.id, m.project_id, m.event_type::text, m.payload_json::text, m.occurred_at, m.attempts
                    )
                    SELECT u.id, u.project_id, u.event_type, u.payload_json, u.occurred_at, u.attempts
                    FROM updated AS u
                    ORDER BY u.occurred_at ASC;
                END;
                $$;

                CREATE OR REPLACE FUNCTION public.fn_purge_expired_idempotency_keys(p_batch_size integer)
                RETURNS integer
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path = pg_catalog, public
                AS $$
                #variable_conflict use_column
                DECLARE
                    v_deleted integer := 0;
                BEGIN
                    WITH doomed AS (
                        SELECT k.id
                        FROM public.idempotency_keys AS k
                        WHERE k.expires_at <= clock_timestamp()
                        ORDER BY k.expires_at ASC
                        LIMIT GREATEST(1, LEAST(p_batch_size, 5000))
                    )
                    DELETE FROM public.idempotency_keys AS t
                    USING doomed AS d
                    WHERE t.id = d.id;

                    GET DIAGNOSTICS v_deleted = ROW_COUNT;
                    RETURN v_deleted;
                END;
                $$;

                REVOKE ALL ON FUNCTION public.fn_resolve_api_key_for_auth(text, text) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION public.fn_resolve_api_key_for_auth(text, text) TO smk_app;

                REVOKE ALL ON FUNCTION public.fn_touch_api_key_last_used(uuid, timestamp with time zone) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION public.fn_touch_api_key_last_used(uuid, timestamp with time zone) TO smk_app;

                REVOKE ALL ON FUNCTION public.fn_resolve_user_project_role_for_auth(uuid, uuid) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION public.fn_resolve_user_project_role_for_auth(uuid, uuid) TO smk_app;

                REVOKE ALL ON FUNCTION public.fn_list_active_project_ids() FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION public.fn_list_active_project_ids() TO smk_app;

                REVOKE ALL ON FUNCTION public.fn_claim_next_document_job(text, integer) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION public.fn_claim_next_document_job(text, integer) TO smk_app;

                REVOKE ALL ON FUNCTION public.fn_renew_document_job_lease(uuid, text, integer) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION public.fn_renew_document_job_lease(uuid, text, integer) TO smk_app;

                REVOKE ALL ON FUNCTION public.fn_claim_outbox_messages(text, integer, integer) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION public.fn_claim_outbox_messages(text, integer, integer) TO smk_app;

                REVOKE ALL ON FUNCTION public.fn_purge_expired_idempotency_keys(integer) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION public.fn_purge_expired_idempotency_keys(integer) TO smk_app;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS public.fn_purge_expired_idempotency_keys(integer);
                DROP FUNCTION IF EXISTS public.fn_claim_outbox_messages(text, integer, integer);
                DROP FUNCTION IF EXISTS public.fn_renew_document_job_lease(uuid, text, integer);
                DROP FUNCTION IF EXISTS public.fn_claim_next_document_job(text, integer);
                DROP FUNCTION IF EXISTS public.fn_list_active_project_ids();
                DROP FUNCTION IF EXISTS public.fn_resolve_user_project_role_for_auth(uuid, uuid);
                DROP FUNCTION IF EXISTS public.fn_touch_api_key_last_used(uuid, timestamp with time zone);
                DROP FUNCTION IF EXISTS public.fn_resolve_api_key_for_auth(text, text);

                DROP POLICY IF EXISTS idempotency_keys_scope_isolation ON public.idempotency_keys;
                ALTER TABLE public.idempotency_keys NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE public.idempotency_keys DISABLE ROW LEVEL SECURITY;
                """);

            string[] controlPlaneDualTables =
            [
                "api_keys",
                "project_quotas",
                "user_project_roles"
            ];

            foreach (var table in controlPlaneDualTables)
            {
                migrationBuilder.Sql($"""
                    DROP POLICY IF EXISTS {table}_tenant_or_control_plane ON public.{table};
                    ALTER TABLE public.{table} NO FORCE ROW LEVEL SECURITY;
                    ALTER TABLE public.{table} DISABLE ROW LEVEL SECURITY;
                    """);
            }

            string[] strictChildTables =
            [
                "template_versions",
                "field_mappings",
                "template_datasets",
                "document_versions",
                "document_job_items"
            ];

            foreach (var table in strictChildTables)
            {
                migrationBuilder.Sql($"""
                    DROP POLICY IF EXISTS {table}_tenant_isolation ON public.{table};
                    ALTER TABLE public.{table} NO FORCE ROW LEVEL SECURITY;
                    ALTER TABLE public.{table} DISABLE ROW LEVEL SECURITY;
                    """);
            }

            string[] strictTenantTables =
            [
                "templates",
                "data_connections",
                "datasets",
                "documents",
                "generation_logs",
                "document_jobs"
            ];

            foreach (var table in strictTenantTables)
            {
                migrationBuilder.Sql($"""
                    DROP POLICY IF EXISTS {table}_tenant_isolation ON public.{table};
                    ALTER TABLE public.{table} NO FORCE ROW LEVEL SECURITY;
                    ALTER TABLE public.{table} DISABLE ROW LEVEL SECURITY;
                    """);
            }

            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS public.smk_is_control_plane();
                DROP FUNCTION IF EXISTS public.smk_current_project_id();
                """);
        }
    }
}
