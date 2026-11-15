using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Contexts;
using SmkDoc.Infrastructure.Persistence.Queries;
using SmkDoc.IntegrationTests.Fixtures;

namespace SmkDoc.IntegrationTests.Persistence;

[Trait("Category", "Integration")]
[Collection(PostgresIntegrationSuiteDefinition.Name)]
public sealed class DocumentJobsPipelineCIntegrationTests(PostgreSqlContainerFixture fixture)
{
    [RequiresPostgreSqlFact]
    public async Task FnClaimNextDocumentJob_ConcurrentWorkersWithSkipLocked_NeverDoubleClaim()
    {
        var pair = await PostgresIntegrationTestSupport.SeedTwoProjectsAsync(fixture, "JobClaim");
        var now = TimeProvider.System.GetUtcNow();
        string suffix = Guid.CreateVersion7().ToString("N")[^10..];

        var tplA = await PostgresIntegrationTestSupport.SeedTemplateAsync(
            fixture, pair.ProjectA.Id, $"Job Tpl A {suffix}", $"job-a-{suffix}");

        // Clear any stray non-terminal jobs from earlier tests in the shared collection DB.
        await using (var adminDs = new NpgsqlDataSourceBuilder(fixture.ConnectionString).Build())
        {
            await using var cleanConn = await adminDs.OpenConnectionAsync();
            await using var cleanCmd = cleanConn.CreateCommand();
            cleanCmd.CommandText = """
                UPDATE public.document_jobs
                SET "Status" = 'Canceled', "LockedUntil" = NULL, "LockedBy" = NULL
                WHERE "Status" IN ('Queued', 'Retrying', 'Running', 'Pending');
                """;
            await cleanCmd.ExecuteNonQueryAsync();
        }

        var job1 = DocumentJob.Create(
            pair.ProjectA.Id,
            tplA.Id,
            tplA.Slug.Value,
            null,
            "job-enqueuer",
            OutputFormat.Pdf,
            [("{\"seq\":1}", "doc-1")],
            now);
        var job2 = DocumentJob.Create(
            pair.ProjectA.Id,
            tplA.Id,
            tplA.Slug.Value,
            null,
            "job-enqueuer",
            OutputFormat.Pdf,
            [("{\"seq\":2}", "doc-2")],
            now.AddMilliseconds(10));

        await using (var adminDs = new NpgsqlDataSourceBuilder(fixture.ConnectionString).Build())
        {
            var ctx = new CurrentProjectContext();
            ctx.SetProject(pair.ProjectA.Id, "job-seed");
            await using var db = PostgresIntegrationTestSupport.CreateDbContext(adminDs, ctx);
            db.DocumentJobs.AddRange(job1, job2);
            await db.SaveChangesAsync();
        }

        await using var appDs = new NpgsqlDataSourceBuilder(fixture.AppRoleConnectionString).Build();

        static async Task<Guid?> ClaimOneAsync(NpgsqlDataSource ds, string workerId, Task barrierTask)
        {
            await barrierTask;
            await using var conn = await ds.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT \"Id\" FROM public.fn_claim_next_document_job(@p_worker, @p_lease);";
            cmd.Parameters.Add(new NpgsqlParameter("p_worker", NpgsqlDbType.Text) { Value = workerId });
            cmd.Parameters.Add(new NpgsqlParameter("p_lease", NpgsqlDbType.Integer) { Value = 60 });
            object? result = await cmd.ExecuteScalarAsync();
            return result is Guid id ? id : null;
        }

        var claimedIds = await ClaimConcurrentlyAsync(appDs);
        claimedIds.Should().AllSatisfy(id => id.Should().NotBeNull());
        claimedIds.Select(id => id!.Value).Should().OnlyHaveUniqueItems("FOR UPDATE SKIP LOCKED must prevent double-claiming");
        claimedIds.Select(id => id!.Value).Should().BeEquivalentTo([job1.Id.Value, job2.Id.Value]);

        // Third claim when queue is empty returns null.
        (await ClaimOneAsync(appDs, "worker-gamma", Task.CompletedTask)).Should().BeNull();

        static async Task<Guid?[]> ClaimConcurrentlyAsync(NpgsqlDataSource ds)
        {
            var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var w1 = ClaimOneAsync(ds, "worker-alpha", barrier.Task);
            var w2 = ClaimOneAsync(ds, "worker-beta", barrier.Task);
            barrier.SetResult();
            return await Task.WhenAll(w1, w2);
        }
    }

    [RequiresPostgreSqlFact]
    public async Task FnRenewDocumentJobLease_ReportsCancelRequested_AndReadModelCursorPaginationIsStable()
    {
        var pair = await PostgresIntegrationTestSupport.SeedTwoProjectsAsync(fixture, "JobPage");
        var now = TimeProvider.System.GetUtcNow();
        string suffix = Guid.CreateVersion7().ToString("N")[^10..];

        var tplA = await PostgresIntegrationTestSupport.SeedTemplateAsync(
            fixture, pair.ProjectA.Id, $"Page Tpl {suffix}", $"page-{suffix}");

        await using (var adminDs = new NpgsqlDataSourceBuilder(fixture.ConnectionString).Build())
        {
            await using var cleanConn = await adminDs.OpenConnectionAsync();
            await using var cleanCmd = cleanConn.CreateCommand();
            cleanCmd.CommandText = """
                UPDATE public.document_jobs
                SET "Status" = 'Canceled', "LockedUntil" = NULL, "LockedBy" = NULL
                WHERE "Status" IN ('Queued', 'Retrying', 'Running', 'Pending');
                """;
            await cleanCmd.ExecuteNonQueryAsync();
        }

        var job1 = DocumentJob.Create(
            pair.ProjectA.Id, tplA.Id, tplA.Slug.Value, null, "pager", OutputFormat.Pdf, [("{\"n\":1}", "p1")], now);
        var job2 = DocumentJob.Create(
            pair.ProjectA.Id, tplA.Id, tplA.Slug.Value, null, "pager", OutputFormat.Pdf, [("{\"n\":2}", "p2")], now.AddSeconds(1));

        await using var appDs = new NpgsqlDataSourceBuilder(fixture.AppRoleConnectionString).Build();
        var ctxA = new CurrentProjectContext();
        ctxA.SetProject(pair.ProjectA.Id, "job-page");

        await using (var db = PostgresIntegrationTestSupport.CreateDbContext(appDs, ctxA))
        {
            db.DocumentJobs.AddRange(job1, job2);
            await db.SaveChangesAsync();
        }

        // Claim job1 via fn_claim_next_document_job, then mark CancelRequested and renew lease via fn_renew_document_job_lease.
        Guid claimedJobId;
        await using (var conn = await appDs.OpenConnectionAsync())
        {
            await using var claimCmd = conn.CreateCommand();
            claimCmd.CommandText = "SELECT \"Id\" FROM public.fn_claim_next_document_job('worker-lease', 60);";
            claimedJobId = (Guid)(await claimCmd.ExecuteScalarAsync())!;
        }

        await using (var db = PostgresIntegrationTestSupport.CreateDbContext(appDs, ctxA))
        {
            var targetJobId = new DocumentJobId(claimedJobId);
            var running = await db.DocumentJobs.FirstAsync(j => j.Id == targetJobId);
            running.RequestCancel(now.AddSeconds(2));
            await db.SaveChangesAsync();
        }

        await using (var conn = await appDs.OpenConnectionAsync())
        {
            await using var renewCmd = conn.CreateCommand();
            renewCmd.CommandText = "SELECT \"Renewed\", \"CancelRequested\" FROM public.fn_renew_document_job_lease(@jid, 'worker-lease', 90);";
            renewCmd.Parameters.Add(new NpgsqlParameter("jid", NpgsqlDbType.Uuid) { Value = claimedJobId });
            await using var reader = await renewCmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
            (await reader.ReadAsync()).Should().BeTrue();
            reader.GetBoolean(0).Should().BeTrue();
            reader.GetBoolean(1).Should().BeTrue("cooperative cancellation flag must surface on lease heartbeat");
        }

        // Cursor pagination across the 2 jobs in Project A using DocumentJobReadModel under smk_app.
        await using (var readDb = PostgresIntegrationTestSupport.CreateDbContext(appDs, ctxA))
        {
            var readModel = new DocumentJobReadModel(readDb);
            var page1 = await readModel.ListPagedAsync(null, null, limit: 1, CancellationToken.None);
            page1.Items.Should().ContainSingle();
            page1.HasMore.Should().BeTrue();
            page1.NextCursor.Should().NotBeNullOrWhiteSpace();

            var page2 = await readModel.ListPagedAsync(null, page1.NextCursor, limit: 1, CancellationToken.None);
            page2.Items.Should().ContainSingle();
            page2.HasMore.Should().BeFalse();

            var combinedIds = page1.Items.Concat(page2.Items).Select(x => x.JobId).ToList();
            combinedIds.Should().BeEquivalentTo([job1.Id.Value, job2.Id.Value]);
        }
    }
}
