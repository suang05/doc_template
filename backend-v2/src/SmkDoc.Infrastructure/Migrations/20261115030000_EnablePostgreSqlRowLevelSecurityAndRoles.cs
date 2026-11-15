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

            // Strict tenant-scoped root tables
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
                    ALTER TABLE public."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE public."{table}" FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS "{table}_tenant_isolation" ON public."{table}";
                    CREATE POLICY "{table}_tenant_isolation" ON public."{table}"
                        AS PERMISSIVE
                        FOR ALL
                        TO PUBLIC
                        USING ("ProjectId" = public.smk_current_project_id())
                        WITH CHECK ("ProjectId" = public.smk_current_project_id());
                    """);
            }

            // Strict tenant-scoped child tables (parent join)
            migrationBuilder.Sql("""
                ALTER TABLE public."template_versions" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE public."template_versions" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS "template_versions_tenant_isolation" ON public."template_versions";
                CREATE POLICY "template_versions_tenant_isolation" ON public."template_versions"
                    AS PERMISSIVE FOR ALL TO PUBLIC
                    USING (EXISTS (SELECT 1 FROM public.templates AS t WHERE t."Id" = "TemplateId" AND t."ProjectId" = public.smk_current_project_id()))
                    WITH CHECK (EXISTS (SELECT 1 FROM public.templates AS t WHERE t."Id" = "TemplateId" AND t."ProjectId" = public.smk_current_project_id()));

                ALTER TABLE public."field_mappings" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE public."field_mappings" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS "field_mappings_tenant_isolation" ON public."field_mappings";
                CREATE POLICY "field_mappings_tenant_isolation" ON public."field_mappings"
                    AS PERMISSIVE FOR ALL TO PUBLIC
                    USING (EXISTS (SELECT 1 FROM public.templates AS t WHERE t."Id" = "TemplateId" AND t."ProjectId" = public.smk_current_project_id()))
                    WITH CHECK (EXISTS (SELECT 1 FROM public.templates AS t WHERE t."Id" = "TemplateId" AND t."ProjectId" = public.smk_current_project_id()));

                ALTER TABLE public."template_datasets" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE public."template_datasets" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS "template_datasets_tenant_isolation" ON public."template_datasets";
                CREATE POLICY "template_datasets_tenant_isolation" ON public."template_datasets"
                    AS PERMISSIVE FOR ALL TO PUBLIC
                    USING (EXISTS (SELECT 1 FROM public.templates AS t WHERE t."Id" = "TemplateId" AND t."ProjectId" = public.smk_current_project_id()))
                    WITH CHECK (EXISTS (SELECT 1 FROM public.templates AS t WHERE t."Id" = "TemplateId" AND t."ProjectId" = public.smk_current_project_id()));

                ALTER TABLE public."document_versions" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE public."document_versions" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS "document_versions_tenant_isolation" ON public."document_versions";
                CREATE POLICY "document_versions_tenant_isolation" ON public."document_versions"
                    AS PERMISSIVE FOR ALL TO PUBLIC
                    USING (EXISTS (SELECT 1 FROM public.documents AS d WHERE d."Id" = "DocumentId" AND d."ProjectId" = public.smk_current_project_id()))
                    WITH CHECK (EXISTS (SELECT 1 FROM public.documents AS d WHERE d."Id" = "DocumentId" AND d."ProjectId" = public.smk_current_project_id()));

                ALTER TABLE public."document_job_items" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE public."document_job_items" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS "document_job_items_tenant_isolation" ON public."document_job_items";
                CREATE POLICY "document_job_items_tenant_isolation" ON public."document_job_items"
                    AS PERMISSIVE FOR ALL TO PUBLIC
                    USING (EXISTS (SELECT 1 FROM public.document_jobs AS j WHERE j."Id" = "DocumentJobId" AND j."ProjectId" = public.smk_current_project_id()))
                    WITH CHECK (EXISTS (SELECT 1 FROM public.document_jobs AS j WHERE j."Id" = "DocumentJobId" AND j."ProjectId" = public.smk_current_project_id()));
                """);

            // Dual tenant / Control Plane tables
            string[] controlPlaneDualTables =
            [
                "api_keys",
                "project_quotas",
                "user_project_roles"
            ];

            foreach (var table in controlPlaneDualTables)
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE public."{table}" ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE public."{table}" FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS "{table}_tenant_or_control_plane" ON public."{table}";
                    CREATE POLICY "{table}_tenant_or_control_plane" ON public."{table}"
                        AS PERMISSIVE
                        FOR ALL
                        TO PUBLIC
                        USING (
                            ("ProjectId" = public.smk_current_project_id())
                            OR (public.smk_current_project_id() IS NULL AND public.smk_is_control_plane())
                        )
                        WITH CHECK (
                            ("ProjectId" = public.smk_current_project_id())
                            OR (public.smk_current_project_id() IS NULL AND public.smk_is_control_plane())
                        );
                    """);
            }

            // Durable idempotency keys RLS (tenant-isolated OR control-plane when ProjectId IS NULL)
            migrationBuilder.Sql("""
                ALTER TABLE public."idempotency_keys" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE public."idempotency_keys" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS "idempotency_keys_scope_isolation" ON public."idempotency_keys";
                CREATE POLICY "idempotency_keys_scope_isolation" ON public."idempotency_keys"
                    AS PERMISSIVE
                    FOR ALL
                    TO PUBLIC
                    USING (
                        ("ProjectId" IS NOT NULL AND "ProjectId" = public.smk_current_project_id())
                        OR ("ProjectId" IS NULL AND public.smk_current_project_id() IS NULL AND public.smk_is_control_plane())
                    )
                    WITH CHECK (
                        ("ProjectId" IS NOT NULL AND "ProjectId" = public.smk_current_project_id())
                        OR ("ProjectId" IS NULL AND public.smk_current_project_id() IS NULL AND public.smk_is_control_plane())
                    );
                """);

            // Narrow SECURITY DEFINER functions for auth lookups and atomic worker claiming
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION public.fn_resolve_api_key_for_auth(
                    p_key_id text,
                    p_legacy_hash text)
                RETURNS TABLE (
                    "Id" uuid,
                    "ProjectId" uuid,
                    "CallerApp" text,
                    "Scope" text,
                    "IsActive" boolean,
                    "ExpiresAt" timestamp with time zone,
                    "SecretHashBytes" bytea,
                    "KeyHash" text
                )
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path = pg_catalog, public
                AS $$
                BEGIN
                    RETURN QUERY
                    SELECT k."Id",
                           k."ProjectId",
                           k."CallerApp"::text,
                           k."Scope"::text,
                           k."IsActive",
                           k."ExpiresAt",
                           k."SecretHashBytes",
                           k."KeyHash"::text
                    FROM public.api_keys AS k
                    WHERE (p_key_id IS NOT NULL AND k."KeyId" = p_key_id)
                       OR (p_key_id IS NULL AND p_legacy_hash IS NOT NULL AND k."SecretHashBytes" IS NULL AND k."KeyHash" = p_legacy_hash)
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
                BEGIN
                    UPDATE public.api_keys AS k
                    SET "LastUsedAt" = p_now
                    WHERE k."Id" = p_id;
                END;
                $$;

                CREATE OR REPLACE FUNCTION public.fn_resolve_user_project_role_for_auth(
                    p_project_id uuid,
                    p_user_id uuid)
                RETURNS TABLE (
                    "ProjectId" uuid,
                    "UserId" uuid,
                    "Role" text
                )
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path = pg_catalog, public
                AS $$
                BEGIN
                    RETURN QUERY
                    SELECT upr."ProjectId",
                           upr."UserId",
                           upr."Role"::text
                    FROM public.user_project_roles AS upr
                    WHERE upr."ProjectId" = p_project_id
                      AND upr."UserId" = p_user_id
                    LIMIT 1;
                END;
                $$;

                CREATE OR REPLACE FUNCTION public.fn_list_active_project_ids()
                RETURNS TABLE ("ProjectId" uuid)
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path = pg_catalog, public
                AS $$
                BEGIN
                    RETURN QUERY
                    SELECT p."Id"
                    FROM public.projects AS p
                    WHERE p."IsActive" = true
                    ORDER BY p."CreatedAt" ASC;
                END;
                $$;

                CREATE OR REPLACE FUNCTION public.fn_claim_next_document_job(
                    p_worker_id text,
                    p_lease_seconds integer)
                RETURNS TABLE (
                    "Id" uuid,
                    "ProjectId" uuid
                )
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path = pg_catalog, public
                AS $$
                DECLARE
                    v_now timestamp with time zone := clock_timestamp();
                BEGIN
                    RETURN QUERY
                    WITH candidate AS (
                        SELECT j."Id"
                        FROM public.document_jobs AS j
                        WHERE (j."Status" IN ('Queued', 'Retrying') AND j."AvailableAt" <= v_now)
                           OR (j."Status" IN ('Running', 'Pending') AND j."LockedUntil" IS NOT NULL AND j."LockedUntil" <= v_now)
                        ORDER BY j."AvailableAt" ASC, j."CreatedAt" ASC
                        LIMIT 1
                        FOR UPDATE SKIP LOCKED
                    ),
                    claimed AS (
                        UPDATE public.document_jobs AS j
                        SET "Status" = 'Running',
                            "Attempt" = j."Attempt" + 1,
                            "LockedBy" = p_worker_id,
                            "LockedUntil" = v_now + make_interval(secs => GREATEST(10, p_lease_seconds)),
                            "StartedAt" = COALESCE(j."StartedAt", v_now),
                            "UpdatedAt" = v_now,
                            "Version" = j."Version" + 1
                        FROM candidate AS c
                        WHERE j."Id" = c."Id"
                        RETURNING j."Id", j."ProjectId"
                    )
                    SELECT cl."Id", cl."ProjectId"
                    FROM claimed AS cl;
                END;
                $$;

                CREATE OR REPLACE FUNCTION public.fn_renew_document_job_lease(
                    p_job_id uuid,
                    p_worker_id text,
                    p_lease_seconds integer)
                RETURNS TABLE (
                    "Renewed" boolean,
                    "CancelRequested" boolean
                )
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path = pg_catalog, public
                AS $$
                DECLARE
                    v_now timestamp with time zone := clock_timestamp();
                BEGIN
                    RETURN QUERY
                    UPDATE public.document_jobs AS j
                    SET "LockedUntil" = v_now + make_interval(secs => GREATEST(10, p_lease_seconds)),
                        "UpdatedAt" = v_now
                    WHERE j."Id" = p_job_id
                      AND j."LockedBy" = p_worker_id
                      AND j."Status" = 'Running'
                    RETURNING true AS "Renewed", j."CancelRequested";
                END;
                $$;

                CREATE OR REPLACE FUNCTION public.fn_claim_outbox_messages(
                    p_worker_id text,
                    p_batch_size integer,
                    p_lease_seconds integer)
                RETURNS TABLE (
                    "Id" uuid,
                    "ProjectId" uuid,
                    "EventType" text,
                    "PayloadJson" text,
                    "OccurredAt" timestamp with time zone,
                    "Attempts" integer
                )
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path = pg_catalog, public
                AS $$
                DECLARE
                    v_now timestamp with time zone := clock_timestamp();
                BEGIN
                    RETURN QUERY
                    WITH candidates AS (
                        SELECT m."Id"
                        FROM public.outbox_messages AS m
                        WHERE m."ProcessedAt" IS NULL
                          AND m."DeadLetteredAt" IS NULL
                          AND m."AvailableAt" <= v_now
                          AND (m."LockedUntil" IS NULL OR m."LockedUntil" <= v_now)
                        ORDER BY m."AvailableAt" ASC, m."OccurredAt" ASC
                        LIMIT GREATEST(1, LEAST(p_batch_size, 500))
                        FOR UPDATE SKIP LOCKED
                    ),
                    updated AS (
                        UPDATE public.outbox_messages AS m
                        SET "LockedBy" = p_worker_id,
                            "LockedUntil" = v_now + make_interval(secs => GREATEST(5, p_lease_seconds)),
                            "Attempts" = m."Attempts" + 1
                        FROM candidates AS c
                        WHERE m."Id" = c."Id"
                        RETURNING m."Id", m."ProjectId", m."EventType"::text, m."PayloadJson"::text, m."OccurredAt", m."Attempts"
                    )
                    SELECT u."Id", u."ProjectId", u."EventType", u."PayloadJson", u."OccurredAt", u."Attempts"
                    FROM updated AS u
                    ORDER BY u."OccurredAt" ASC;
                END;
                $$;

                CREATE OR REPLACE FUNCTION public.fn_purge_expired_idempotency_keys(p_batch_size integer)
                RETURNS integer
                LANGUAGE plpgsql
                SECURITY DEFINER
                SET search_path = pg_catalog, public
                AS $$
                DECLARE
                    v_deleted integer := 0;
                BEGIN
                    WITH doomed AS (
                        SELECT k."Id"
                        FROM public.idempotency_keys AS k
                        WHERE k."ExpiresAt" <= clock_timestamp()
                        ORDER BY k."ExpiresAt" ASC
                        LIMIT GREATEST(1, LEAST(p_batch_size, 5000))
                    )
                    DELETE FROM public.idempotency_keys AS t
                    USING doomed AS d
                    WHERE t."Id" = d."Id";

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

                DROP POLICY IF EXISTS "idempotency_keys_scope_isolation" ON public."idempotency_keys";
                ALTER TABLE public."idempotency_keys" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE public."idempotency_keys" DISABLE ROW LEVEL SECURITY;
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
                    DROP POLICY IF EXISTS "{table}_tenant_or_control_plane" ON public."{table}";
                    ALTER TABLE public."{table}" NO FORCE ROW LEVEL SECURITY;
                    ALTER TABLE public."{table}" DISABLE ROW LEVEL SECURITY;
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
                    DROP POLICY IF EXISTS "{table}_tenant_isolation" ON public."{table}";
                    ALTER TABLE public."{table}" NO FORCE ROW LEVEL SECURITY;
                    ALTER TABLE public."{table}" DISABLE ROW LEVEL SECURITY;
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
                    DROP POLICY IF EXISTS "{table}_tenant_isolation" ON public."{table}";
                    ALTER TABLE public."{table}" NO FORCE ROW LEVEL SECURITY;
                    ALTER TABLE public."{table}" DISABLE ROW LEVEL SECURITY;
                    """);
            }

            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS public.smk_is_control_plane();
                DROP FUNCTION IF EXISTS public.smk_current_project_id();
                """);
        }
    }
}
