using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Infrastructure.Contexts;
using SmkDoc.Infrastructure.Security;
using SmkDoc.IntegrationTests.Fixtures;

namespace SmkDoc.IntegrationTests.Persistence;

[Trait("Category", "Integration")]
[Collection(PostgresIntegrationSuiteDefinition.Name)]
public sealed class PostgresRlsIsolationTests(PostgreSqlContainerFixture fixture)
{
    [RequiresPostgreSqlFact]
    public async Task PgRoles_SmkAppRole_ExistsWithoutSuperuserOrBypassRls()
    {
        await using var adminDs = new NpgsqlDataSourceBuilder(fixture.ConnectionString).Build();
        await using var conn = await adminDs.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT rolsuper, rolbypassrls, rolcanlogin
            FROM pg_roles
            WHERE rolname = 'smk_app';
            """;

        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue("smk_app role must be provisioned by RLS migration");
        reader.GetBoolean(0).Should().BeFalse("smk_app must not be superuser");
        reader.GetBoolean(1).Should().BeFalse("smk_app MUST have NOBYPASSRLS for PostgreSQL RLS enforcement");
        reader.GetBoolean(2).Should().BeTrue("smk_app must allow application login");
    }

    [RequiresPostgreSqlFact]
    public async Task Rls_UnderSmkApp_IsolatesTenantTablesAndChildTablesEvenOnRawSqlWithoutEfFilters()
    {
        var pair = await PostgresIntegrationTestSupport.SeedTwoProjectsAsync(fixture, "RlsIso");
        var now = TimeProvider.System.GetUtcNow();
        string suffix = Guid.CreateVersion7().ToString("N")[^10..];

        var templateA = await PostgresIntegrationTestSupport.SeedTemplateAsync(
            fixture, pair.ProjectA.Id, $"Tpl A {suffix}", $"tpl-a-{suffix}");
        var templateB = await PostgresIntegrationTestSupport.SeedTemplateAsync(
            fixture, pair.ProjectB.Id, $"Tpl B {suffix}", $"tpl-b-{suffix}");

        // Seed child-table entities (DocumentJob + DocumentJobItem) for both tenants.
        DocumentJob jobA;
        DocumentJob jobB;
        await using (var adminDs = new NpgsqlDataSourceBuilder(fixture.ConnectionString).Build())
        {
            var ctxA = new CurrentProjectContext();
            ctxA.SetProject(pair.ProjectA.Id, "rls-seed-a");
            await using (var dbA = PostgresIntegrationTestSupport.CreateDbContext(adminDs, ctxA))
            {
                jobA = DocumentJob.Create(
                    pair.ProjectA.Id,
                    templateA.Id,
                    templateA.Slug.Value,
                    null,
                    "seed-a",
                    OutputFormat.Pdf,
                    [("{\"name\":\"A\"}", "doc-a-1")],
                    now);
                dbA.DocumentJobs.Add(jobA);
                await dbA.SaveChangesAsync();
            }

            var ctxB = new CurrentProjectContext();
            ctxB.SetProject(pair.ProjectB.Id, "rls-seed-b");
            await using (var dbB = PostgresIntegrationTestSupport.CreateDbContext(adminDs, ctxB))
            {
                jobB = DocumentJob.Create(
                    pair.ProjectB.Id,
                    templateB.Id,
                    templateB.Slug.Value,
                    null,
                    "seed-b",
                    OutputFormat.Pdf,
                    [("{\"name\":\"B\"}", "doc-b-1")],
                    now);
                dbB.DocumentJobs.Add(jobB);
                await dbB.SaveChangesAsync();
            }
        }

        // Now connect strictly as non-privileged role smk_app (NOBYPASSRLS).
        await using var appDs = new NpgsqlDataSourceBuilder(fixture.AppRoleConnectionString).Build();

        // 1. Fail-closed: unset project session context returns ZERO rows on raw SQL (no EF query filters!).
        await using (var unsetConn = await appDs.OpenConnectionAsync())
        {
            await using var cmd = unsetConn.CreateCommand();
            cmd.CommandText = """
                SELECT
                    (SELECT COUNT(*) FROM public.templates WHERE id IN (@ta, @tb)) AS tpl_count,
                    (SELECT COUNT(*) FROM public.document_jobs WHERE id IN (@ja, @jb)) AS job_count,
                    (SELECT COUNT(*) FROM public.document_job_items WHERE document_job_id IN (@ja, @jb)) AS item_count;
                """;
            cmd.Parameters.Add(new NpgsqlParameter("ta", NpgsqlDbType.Uuid) { Value = templateA.Id.Value });
            cmd.Parameters.Add(new NpgsqlParameter("tb", NpgsqlDbType.Uuid) { Value = templateB.Id.Value });
            cmd.Parameters.Add(new NpgsqlParameter("ja", NpgsqlDbType.Uuid) { Value = jobA.Id.Value });
            cmd.Parameters.Add(new NpgsqlParameter("jb", NpgsqlDbType.Uuid) { Value = jobB.Id.Value });

            await using var reader = await cmd.ExecuteReaderAsync();
            (await reader.ReadAsync()).Should().BeTrue();
            reader.GetInt64(0).Should().Be(0, "unset session must see zero templates under RLS");
            reader.GetInt64(1).Should().Be(0, "unset session must see zero document_jobs under RLS");
            reader.GetInt64(2).Should().Be(0, "unset session must see zero document_job_items under child RLS");
        }

        // 2. Scoped to Project A via transaction-local set_config returns ONLY Project A across raw SQL + EF Core.
        await using (var scopedConn = await appDs.OpenConnectionAsync())
        {
            await using var tx = await scopedConn.BeginTransactionAsync();
            await using (var cfg = scopedConn.CreateCommand())
            {
                cfg.Transaction = tx;
                cfg.CommandText = "SELECT set_config('app.current_project_id', @pid, true), set_config('app.control_plane_context', '', true);";
                cfg.Parameters.Add(new NpgsqlParameter("pid", NpgsqlDbType.Text) { Value = pair.ProjectA.Id.Value.ToString("D") });
                await cfg.ExecuteNonQueryAsync();
            }

            await using (var rawCmd = scopedConn.CreateCommand())
            {
                rawCmd.Transaction = tx;
                rawCmd.CommandText = """
                    SELECT
                        (SELECT COUNT(*) FROM public.templates WHERE id = @ta) AS own_tpl,
                        (SELECT COUNT(*) FROM public.templates WHERE id = @tb) AS cross_tpl,
                        (SELECT COUNT(*) FROM public.document_job_items WHERE document_job_id = @ja) AS own_items,
                        (SELECT COUNT(*) FROM public.document_job_items WHERE document_job_id = @jb) AS cross_items;
                    """;
                rawCmd.Parameters.Add(new NpgsqlParameter("ta", NpgsqlDbType.Uuid) { Value = templateA.Id.Value });
                rawCmd.Parameters.Add(new NpgsqlParameter("tb", NpgsqlDbType.Uuid) { Value = templateB.Id.Value });
                rawCmd.Parameters.Add(new NpgsqlParameter("ja", NpgsqlDbType.Uuid) { Value = jobA.Id.Value });
                rawCmd.Parameters.Add(new NpgsqlParameter("jb", NpgsqlDbType.Uuid) { Value = jobB.Id.Value });

                await using var reader = await rawCmd.ExecuteReaderAsync();
                (await reader.ReadAsync()).Should().BeTrue();
                reader.GetInt64(0).Should().Be(1);
                reader.GetInt64(1).Should().Be(0, "Project A session must NEVER see Project B template even in raw SQL");
                reader.GetInt64(2).Should().Be(1);
                reader.GetInt64(3).Should().Be(0, "Project A session must NEVER see Project B child items under EXISTS RLS");
            }
        }

        // 3. Full EF Core stack with TenantDbConnectionInterceptor + TenantDbTransactionInterceptor on smk_app.
        var efCtxA = new CurrentProjectContext();
        efCtxA.SetProject(pair.ProjectA.Id, "ef-rls-a");
        await using var efDbA = PostgresIntegrationTestSupport.CreateDbContext(appDs, efCtxA);

        var visibleTemplateIds = await efDbA.Templates
            .AsNoTracking()
            .Where(t => t.Id == templateA.Id || t.Id == templateB.Id)
            .Select(t => t.Id)
            .ToListAsync();

        visibleTemplateIds.Should().ContainSingle().Which.Should().Be(templateA.Id);
    }

    [RequiresPostgreSqlFact]
    public async Task Rls_DualControlPlaneTables_AndSecurityDefinerApiKeyAuth_WorkUnderSmkApp()
    {
        var pair = await PostgresIntegrationTestSupport.SeedTwoProjectsAsync(fixture, "RlsDual");
        var now = TimeProvider.System.GetUtcNow();
        var apiKeyOptions = Options.Create(new ApiKeyOptions
        {
            ApiKeyPepper = "integration-test-hmac-pepper-secret-0123456789",
            AllowLegacySha256Keys = false
        });
        var generator = new ApiKeyCredentialGenerator(apiKeyOptions);
        var mintedA = generator.MintLiveKey();
        var mintedB = generator.MintLiveKey();

        ApiKey keyA;
        ApiKey keyB;
        await using (var adminDs = new NpgsqlDataSourceBuilder(fixture.ConnectionString).Build())
        {
            var cpCtx = new CurrentProjectContext();
            cpCtx.SetControlPlane("dual-seed");
            await using var db = PostgresIntegrationTestSupport.CreateDbContext(adminDs, cpCtx);

            keyA = ApiKey.IssueStructured(
                pair.ProjectA.Id,
                ApiKeyName.Create("ERP Key A"),
                "erp-a",
                mintedA.KeyPrefix,
                mintedA.KeyId,
                mintedA.KeySuffix,
                mintedA.SecretHash.ToArray(),
                ExpirationPolicy.Never,
                now);
            keyB = ApiKey.IssueStructured(
                pair.ProjectB.Id,
                ApiKeyName.Create("ERP Key B"),
                "erp-b",
                mintedB.KeyPrefix,
                mintedB.KeyId,
                mintedB.KeySuffix,
                mintedB.SecretHash.ToArray(),
                ExpirationPolicy.Never,
                now);

            db.ApiKeys.AddRange(keyA, keyB);
            await db.SaveChangesAsync();
        }

        await using var appDs = new NpgsqlDataSourceBuilder(fixture.AppRoleConnectionString).Build();

        // Unset context under smk_app sees zero rows in api_keys & project_quotas.
        await using (var unsetConn = await appDs.OpenConnectionAsync())
        {
            await using var cmd = unsetConn.CreateCommand();
            cmd.CommandText = """
                SELECT
                    (SELECT COUNT(*) FROM public.api_keys WHERE id IN (@ka, @kb)),
                    (SELECT COUNT(*) FROM public.project_quotas WHERE project_id IN (@pa, @pb));
                """;
            cmd.Parameters.Add(new NpgsqlParameter("ka", NpgsqlDbType.Uuid) { Value = keyA.Id.Value });
            cmd.Parameters.Add(new NpgsqlParameter("kb", NpgsqlDbType.Uuid) { Value = keyB.Id.Value });
            cmd.Parameters.Add(new NpgsqlParameter("pa", NpgsqlDbType.Uuid) { Value = pair.ProjectA.Id.Value });
            cmd.Parameters.Add(new NpgsqlParameter("pb", NpgsqlDbType.Uuid) { Value = pair.ProjectB.Id.Value });

            await using var r = await cmd.ExecuteReaderAsync();
            (await r.ReadAsync()).Should().BeTrue();
            r.GetInt64(0).Should().Be(0);
            r.GetInt64(1).Should().Be(0);
        }

        // Control Plane context sees BOTH projects' api_keys & quotas.
        await using (var cpConn = await appDs.OpenConnectionAsync())
        {
            await using var tx = await cpConn.BeginTransactionAsync();
            await using (var cfg = cpConn.CreateCommand())
            {
                cfg.Transaction = tx;
                cfg.CommandText = "SELECT set_config('app.current_project_id', '', true), set_config('app.control_plane_context', 'true', true);";
                await cfg.ExecuteNonQueryAsync();
            }

            await using var cmd = cpConn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                SELECT
                    (SELECT COUNT(*) FROM public.api_keys WHERE id IN (@ka, @kb)),
                    (SELECT COUNT(*) FROM public.project_quotas WHERE project_id IN (@pa, @pb));
                """;
            cmd.Parameters.Add(new NpgsqlParameter("ka", NpgsqlDbType.Uuid) { Value = keyA.Id.Value });
            cmd.Parameters.Add(new NpgsqlParameter("kb", NpgsqlDbType.Uuid) { Value = keyB.Id.Value });
            cmd.Parameters.Add(new NpgsqlParameter("pa", NpgsqlDbType.Uuid) { Value = pair.ProjectA.Id.Value });
            cmd.Parameters.Add(new NpgsqlParameter("pb", NpgsqlDbType.Uuid) { Value = pair.ProjectB.Id.Value });

            await using var r = await cmd.ExecuteReaderAsync();
            (await r.ReadAsync()).Should().BeTrue();
            r.GetInt64(0).Should().Be(2);
            r.GetInt64(1).Should().Be(2);
        }

        // SECURITY DEFINER auth lookup resolves plain-text structured API key as smk_app without active tenant GUC.
        var verifier = new ApiKeyVerifier(apiKeyOptions, TimeProvider.System, appDs);
        var verified = await verifier.VerifyAsync(mintedA.PlainTextKey, CancellationToken.None);

        verified.Should().NotBeNull();
        verified!.KeyId.Should().Be(keyA.Id);
        verified.ProjectId.Should().Be(pair.ProjectA.Id);
        verified.CallerApp.Should().Be("erp-a");
        verified.IsLegacySha256.Should().BeFalse();

        await verifier.TouchUsageAsync(keyA.Id, now.AddMinutes(1), CancellationToken.None);

        // Wrong secret with same KeyId must return null cleanly.
        var tamperedKey = $"{mintedA.KeyPrefix}_{mintedA.KeyId}_WrongSecretValue0123456789abcdef";
        (await verifier.VerifyAsync(tamperedKey, CancellationToken.None)).Should().BeNull();
    }

    [RequiresPostgreSqlFact]
    public async Task InformationSchema_AllPublicTablesColumnsAndConstraints_MustBePureLowercaseSnakeCase()
    {
        await using var adminDs = new NpgsqlDataSourceBuilder(fixture.ConnectionString).Build();
        await using var conn = await adminDs.OpenConnectionAsync();

        var violations = new List<string>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT 'table' AS kind, table_name AS ident
                FROM information_schema.tables
                WHERE table_schema = 'public'
                  AND table_type = 'BASE TABLE'
                  AND table_name <> '__EFMigrationsHistory'
                  AND table_name ~ '[A-Z]'
                UNION ALL
                SELECT 'column' AS kind, table_name || '.' || column_name AS ident
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name <> '__EFMigrationsHistory'
                  AND column_name ~ '[A-Z]'
                UNION ALL
                SELECT 'constraint' AS kind, table_name || '.' || constraint_name AS ident
                FROM information_schema.table_constraints
                WHERE constraint_schema = 'public'
                  AND table_name <> '__EFMigrationsHistory'
                  AND constraint_name ~ '[A-Z]'
                UNION ALL
                SELECT 'index' AS kind, tablename || '.' || indexname AS ident
                FROM pg_indexes
                WHERE schemaname = 'public'
                  AND tablename <> '__EFMigrationsHistory'
                  AND indexname ~ '[A-Z]';
                """;

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                violations.Add($"{reader.GetString(0)}:{reader.GetString(1)}");
            }
        }

        violations.Should().BeEmpty(
            $"Live PostgreSQL schema must use 100% lowercase snake_case identifiers. Found: {string.Join(", ", violations)}");
    }
}
