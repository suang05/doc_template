using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using SmkDoc.Domain.Events;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.BackgroundWorkers;
using SmkDoc.Infrastructure.Contexts;
using SmkDoc.Infrastructure.Events;
using SmkDoc.Infrastructure.Persistence.Idempotency;
using SmkDoc.Infrastructure.Persistence.Repositories;
using SmkDoc.IntegrationTests.Fixtures;

namespace SmkDoc.IntegrationTests.Persistence;

[Trait("Category", "Integration")]
[Collection(PostgresIntegrationSuiteDefinition.Name)]
public sealed class OutboxAndIdempotencyIntegrationTests(PostgreSqlContainerFixture fixture)
{
    private sealed class CapturingProjectDeactivatedHandler(
        ProjectId expectedProjectId,
        TaskCompletionSource<ProjectId> signal)
        : IDomainEventHandler<ProjectDeactivatedDomainEvent>
    {
        public Task HandleAsync(ProjectDeactivatedDomainEvent domainEvent, CancellationToken ct)
        {
            if (domainEvent.ProjectId == expectedProjectId)
            {
                signal.TrySetResult(domainEvent.ProjectId);
            }

            return Task.CompletedTask;
        }
    }

    [RequiresPostgreSqlFact]
    public async Task OutboxInterceptor_PersistsEventAtomically_AndRelayWorkerDispatchesAndMarksProcessed()
    {
        var pair = await PostgresIntegrationTestSupport.SeedTwoProjectsAsync(fixture, "Outbox");
        var now = TimeProvider.System.GetUtcNow();

        Guid persistedOutboxId;
        await using (var adminDs = new NpgsqlDataSourceBuilder(fixture.ConnectionString).Build())
        {
            var cpCtx = new CurrentProjectContext();
            cpCtx.SetControlPlane("outbox-producer");

            await using var db = PostgresIntegrationTestSupport.CreateDbContext(adminDs, cpCtx);
            var project = await db.Projects.FirstAsync(p => p.Id == pair.ProjectA.Id);
            project.Deactivate(now);

            var uow = new UnitOfWork(db);
            await uow.CommitAsync(CancellationToken.None);

            var candidates = await db.OutboxMessages
                .AsNoTracking()
                .Where(m => m.ProjectId == null && m.EventType == "project.deactivated.v1")
                .OrderByDescending(m => m.OccurredAt)
                .ToListAsync();

            var msg = candidates.FirstOrDefault(m =>
                m.PayloadJson.Contains(pair.ProjectA.Id.Value.ToString("D"), StringComparison.OrdinalIgnoreCase));

            msg.Should().NotBeNull("OutboxSaveChangesInterceptor must persist ProjectDeactivatedDomainEvent atomically");
            msg!.ProcessedAt.Should().BeNull();
            msg.Attempts.Should().Be(0);
            persistedOutboxId = msg.Id;
        }

        // Start real OutboxRelayWorker backed by PostgreSQL fn_claim_outbox_messages (smk_app role).
        await using var appDs = new NpgsqlDataSourceBuilder(fixture.AppRoleConnectionString).Build();
        var dispatchedSignal = new TaskCompletionSource<ProjectId>(TaskCreationOptions.RunContinuationsAsynchronously);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<CurrentProjectContext>();
        services.AddScoped<ICurrentProjectContext>(sp => sp.GetRequiredService<CurrentProjectContext>());
        services.AddScoped<ITenantScope, TenantScope>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IDomainEventHandler<ProjectDeactivatedDomainEvent>>(
            _ => new CapturingProjectDeactivatedHandler(pair.ProjectA.Id, dispatchedSignal));

        await using var sp = services.BuildServiceProvider();
        var worker = new OutboxRelayWorker(
            sp.GetRequiredService<IServiceScopeFactory>(),
            appDs,
            TimeProvider.System,
            NullLogger<OutboxRelayWorker>.Instance,
            Options.Create(new OutboxWorkerOptions
            {
                PollingIntervalMilliseconds = 50,
                BatchSize = 20,
                LeaseSeconds = 30
            }));

        using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await worker.StartAsync(stopCts.Token);
        try
        {
            var deliveredProjectId = await dispatchedSignal.Task.WaitAsync(TimeSpan.FromSeconds(8));
            deliveredProjectId.Should().Be(pair.ProjectA.Id);

            // Allow MarkProcessedAsync write immediately following handler execution to complete.
            DateTimeOffset? processedAt = null;
            int attempts = 0;
            for (int i = 0; i < 20; i++)
            {
                await using var checkDs = new NpgsqlDataSourceBuilder(fixture.ConnectionString).Build();
                await using var conn = await checkDs.OpenConnectionAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT processed_at, attempts FROM public.outbox_messages WHERE id = @id;";
                cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Uuid) { Value = persistedOutboxId });
                await using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    processedAt = await reader.IsDBNullAsync(0) ? null : await reader.GetFieldValueAsync<DateTimeOffset>(0);
                    attempts = reader.GetInt32(1);
                    if (processedAt.HasValue)
                    {
                        break;
                    }
                }

                await Task.Delay(50, CancellationToken.None);
            }

            processedAt.Should().NotBeNull("OutboxRelayWorker must mark dispatched row processed");
            attempts.Should().BeGreaterThanOrEqualTo(1);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }
    }

    [RequiresPostgreSqlFact]
    public async Task PostgresIdempotencyStore_ConcurrentAcquire_AllowsExactlyOneAndPurgesExpiredKeys()
    {
        var pair = await PostgresIntegrationTestSupport.SeedTwoProjectsAsync(fixture, "Idemp");
        await using var appDs = new NpgsqlDataSourceBuilder(fixture.AppRoleConnectionString).Build();
        var store = new PostgresIdempotencyStore(appDs, TimeProvider.System);

        string uniqueKey = $"idem-{Guid.CreateVersion7():N}";
        const string scope = "POST:/api/v1/documents/render";
        const string credId = "key:integration-test";
        const string fingerprint = "sha256:abcdef0123456789";

        var req = new IdempotencyAcquisitionRequest(
            pair.ProjectA.Id.Value,
            credId,
            scope,
            uniqueKey,
            fingerprint,
            InFlightTtl: TimeSpan.FromSeconds(60),
            CompletedTtl: TimeSpan.FromHours(1));

        // Simultaneous double submission -> partial-conflict UPSERT guarantees strictly 1 acquirer & 1 conflict.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        static async Task<IdempotencyAcquisitionResult> AcquireAsync(
            PostgresIdempotencyStore s,
            IdempotencyAcquisitionRequest r,
            Task gateTask)
        {
            await gateTask;
            return await s.TryAcquireOrGetAsync(r, CancellationToken.None);
        }

        var task1 = AcquireAsync(store, req, gate.Task);
        var task2 = AcquireAsync(store, req, gate.Task);
        gate.SetResult();

        var results = await Task.WhenAll(task1, task2);
        results.Count(r => r.IsAcquired).Should().Be(1, "only one concurrent writer can acquire an idempotency key");
        var conflict = results.Single(r => !r.IsAcquired);
        conflict.ExistingRecord.Should().NotBeNull();
        conflict.ExistingRecord!.Status.Should().Be(IdempotencyStatus.InFlight);
        conflict.ExistingRecord.RequestFingerprint.Should().Be(fingerprint);

        // Save completed response and verify replay returns Completed record.
        await store.SaveCompletedAsync(
            new IdempotencyCompletionRequest(
                pair.ProjectA.Id.Value,
                credId,
                scope,
                uniqueKey,
                fingerprint,
                StatusCode: 201,
                ResponseJson: "{\"ok\":true}",
                Headers: new Dictionary<string, string> { ["Content-Type"] = "application/json" },
                CompletedTtl: TimeSpan.FromHours(1)),
            CancellationToken.None);

        var replay = await store.TryAcquireOrGetAsync(req, CancellationToken.None);
        replay.IsAcquired.Should().BeFalse();
        replay.ExistingRecord!.Status.Should().Be(IdempotencyStatus.Completed);
        replay.ExistingRecord.StatusCode.Should().Be(201);
        replay.ExistingRecord.ResponseJson.Should().Be("{\"ok\": true}");

        // Acquire an already-expired key and verify fn_purge_expired_idempotency_keys deletes only the expired one.
        string expiredKey = $"idem-exp-{Guid.CreateVersion7():N}";
        var expiredReq = req with
        {
            IdempotencyKey = expiredKey,
            InFlightTtl = TimeSpan.FromSeconds(-10),
            CompletedTtl = TimeSpan.FromSeconds(-5)
        };
        (await store.TryAcquireOrGetAsync(expiredReq, CancellationToken.None)).IsAcquired.Should().BeTrue();

        await using var conn = await appDs.OpenConnectionAsync();
        await using var purgeCmd = conn.CreateCommand();
        purgeCmd.CommandText = "SELECT public.fn_purge_expired_idempotency_keys(500);";
        object? deletedScalar = await purgeCmd.ExecuteScalarAsync();
        Convert.ToInt32(deletedScalar, System.Globalization.CultureInfo.InvariantCulture)
            .Should().BeGreaterThanOrEqualTo(1);

        // Non-expired completed key remains intact and still replays.
        var stillReplay = await store.TryAcquireOrGetAsync(req, CancellationToken.None);
        stillReplay.IsAcquired.Should().BeFalse();
        stillReplay.ExistingRecord!.Status.Should().Be(IdempotencyStatus.Completed);
    }
}
