using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Events;
using SmkDoc.Infrastructure.Persistence.Outbox;

namespace SmkDoc.Infrastructure.BackgroundWorkers;

public sealed class OutboxWorkerOptions
{
    public const string SectionName = "OutboxWorker";

    public int PollingIntervalMilliseconds { get; set; } = 500;
    public int BatchSize { get; set; } = 25;
    public int LeaseSeconds { get; set; } = 60;
    public int MaxAttempts { get; set; } = 8;
    public int BaseRetryDelaySeconds { get; set; } = 2;
}

/// <summary>
/// Relays transactional Outbox domain events persisted in <c>outbox_messages</c> using
/// PostgreSQL <c>FOR UPDATE SKIP LOCKED</c> leases, exponential backoff retries, and dead-letter tracking.
/// </summary>
public sealed partial class OutboxRelayWorker(
    IServiceScopeFactory scopeFactory,
    NpgsqlDataSource dataSource,
    TimeProvider timeProvider,
    ILogger<OutboxRelayWorker> logger,
    IOptions<OutboxWorkerOptions>? options = null) : BackgroundService
{
    private readonly string _workerId = $"{Environment.MachineName}-{Environment.ProcessId}-outbox";
    private readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(Math.Max(100, options?.Value.PollingIntervalMilliseconds ?? 500));
    private readonly int _batchSize = Math.Clamp(options?.Value.BatchSize ?? 25, 1, 200);
    private readonly int _leaseSeconds = Math.Clamp(options?.Value.LeaseSeconds ?? 60, 10, 600);
    private readonly int _maxAttempts = Math.Clamp(options?.Value.MaxAttempts ?? 8, 1, 50);
    private readonly int _baseRetryDelaySeconds = Math.Clamp(options?.Value.BaseRetryDelaySeconds ?? 2, 1, 60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogWorkerStarted(logger, _workerId, _batchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            int processedCount = 0;
            try
            {
                processedCount = await ProcessNextBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is DbException or TimeoutException or InvalidOperationException)
            {
                LogLoopFailure(logger, ex);
            }

            if (processedCount < _batchSize)
            {
                try
                {
                    await Task.Delay(_pollInterval, timeProvider, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        LogWorkerStopped(logger);
    }

    internal async Task<int> ProcessNextBatchAsync(CancellationToken ct)
    {
        var claimedMessages = await ClaimBatchAsync(ct);
        if (claimedMessages.Count == 0)
        {
            return 0;
        }

        foreach (var msg in claimedMessages)
        {
            ct.ThrowIfCancellationRequested();
            await DispatchSingleMessageAsync(msg, ct);
        }

        return claimedMessages.Count;
    }

    private async Task<List<OutboxMessage>> ClaimBatchAsync(CancellationToken ct)
    {
        var results = new List<OutboxMessage>();
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT "Id", "ProjectId", "EventType", "PayloadJson", "OccurredAt", "Attempts"
            FROM public.fn_claim_outbox_messages(@p_worker_id, @p_batch_size, @p_lease_seconds);
            """;
        cmd.Parameters.Add(new NpgsqlParameter("p_worker_id", NpgsqlDbType.Text) { Value = _workerId });
        cmd.Parameters.Add(new NpgsqlParameter("p_batch_size", NpgsqlDbType.Integer) { Value = _batchSize });
        cmd.Parameters.Add(new NpgsqlParameter("p_lease_seconds", NpgsqlDbType.Integer) { Value = _leaseSeconds });

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            Guid id = reader.GetGuid(0);
            Guid? projectId = await reader.IsDBNullAsync(1, ct) ? null : reader.GetGuid(1);
            string eventType = reader.GetString(2);
            string payloadJson = reader.GetString(3);
            DateTimeOffset occurredAt = await reader.GetFieldValueAsync<DateTimeOffset>(4, ct);
            int attempts = reader.GetInt32(5);

            results.Add(OutboxMessage.RehydrateClaimed(id, projectId, eventType, payloadJson, occurredAt, attempts));
        }

        return results;
    }

    private async Task DispatchSingleMessageAsync(OutboxMessage msg, CancellationToken ct)
    {
        try
        {
            var domainEvent = OutboxEventRegistry.Deserialize(msg.EventType, msg.PayloadJson);

            await using var scope = scopeFactory.CreateAsyncScope();
            var tenantScope = scope.ServiceProvider.GetService<ITenantScope>();
            using var _ = msg.ProjectId.HasValue
                ? tenantScope?.BeginTenant(new ProjectId(msg.ProjectId.Value), "system:outbox-relay")
                : tenantScope?.BeginControlPlane("system:outbox-relay");

            var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
            if (dispatcher is DomainEventDispatcher concreteDispatcher)
            {
                await concreteDispatcher.DispatchOrThrowAsync([domainEvent], ct);
            }
            else
            {
                await dispatcher.DispatchEventsAsync([domainEvent], ct);
            }

            await MarkProcessedAsync(msg.Id, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is
            ExternalServiceException or
            HttpRequestException or
            TimeoutException or
            DbException or
            IOException or
            InvalidOperationException)
        {
            await MarkFailureOrDeadLetterAsync(msg, ex, ct);
        }
    }

    private async Task MarkProcessedAsync(Guid messageId, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE public.outbox_messages
            SET "ProcessedAt" = @p_now,
                "LockedUntil" = NULL,
                "LockedBy" = NULL,
                "LastError" = NULL
            WHERE "Id" = @p_id;
            """;
        cmd.Parameters.Add(new NpgsqlParameter("p_id", NpgsqlDbType.Uuid) { Value = messageId });
        cmd.Parameters.Add(new NpgsqlParameter("p_now", NpgsqlDbType.TimestampTz) { Value = timeProvider.GetUtcNow() });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task MarkFailureOrDeadLetterAsync(OutboxMessage msg, Exception ex, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        string errorMessage = ex.Message.Length <= 2000 ? ex.Message : ex.Message[..2000];
        bool isDeadLetter = msg.Attempts >= _maxAttempts;

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();

        if (isDeadLetter)
        {
            LogDeadLettered(logger, ex, msg.Id, msg.EventType, msg.Attempts);
            cmd.CommandText = """
                UPDATE public.outbox_messages
                SET "DeadLetteredAt" = @p_now,
                    "LockedUntil" = NULL,
                    "LockedBy" = NULL,
                    "LastError" = @p_error
                WHERE "Id" = @p_id;
                """;
            cmd.Parameters.Add(new NpgsqlParameter("p_id", NpgsqlDbType.Uuid) { Value = msg.Id });
            cmd.Parameters.Add(new NpgsqlParameter("p_now", NpgsqlDbType.TimestampTz) { Value = now });
            cmd.Parameters.Add(new NpgsqlParameter("p_error", NpgsqlDbType.Text) { Value = errorMessage });
        }
        else
        {
            double backoffSeconds = Math.Min(300d, _baseRetryDelaySeconds * Math.Pow(2d, Math.Max(0, msg.Attempts - 1)));
            var nextAvailable = now.AddSeconds(backoffSeconds);
            LogRetryScheduled(logger, ex, msg.Id, msg.EventType, msg.Attempts, nextAvailable);

            cmd.CommandText = """
                UPDATE public.outbox_messages
                SET "AvailableAt" = @p_available_at,
                    "LockedUntil" = NULL,
                    "LockedBy" = NULL,
                    "LastError" = @p_error
                WHERE "Id" = @p_id;
                """;
            cmd.Parameters.Add(new NpgsqlParameter("p_id", NpgsqlDbType.Uuid) { Value = msg.Id });
            cmd.Parameters.Add(new NpgsqlParameter("p_available_at", NpgsqlDbType.TimestampTz) { Value = nextAvailable });
            cmd.Parameters.Add(new NpgsqlParameter("p_error", NpgsqlDbType.Text) { Value = errorMessage });
        }

        await cmd.ExecuteNonQueryAsync(ct);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "OutboxRelayWorker {WorkerId} started with batch size {BatchSize}.")]
    private static partial void LogWorkerStarted(ILogger logger, string workerId, int batchSize);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "OutboxRelayWorker stopped.")]
    private static partial void LogWorkerStopped(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "OutboxRelayWorker loop iteration encountered an infrastructure error.")]
    private static partial void LogLoopFailure(ILogger logger, Exception ex);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Outbox message {MessageId} ({EventType}) failed attempt {Attempts}; scheduled retry at {NextAvailableAt}.")]
    private static partial void LogRetryScheduled(ILogger logger, Exception ex, Guid messageId, string eventType, int attempts, DateTimeOffset nextAvailableAt);

    [LoggerMessage(
        Level = LogLevel.Critical,
        Message = "Outbox message {MessageId} ({EventType}) exhausted {Attempts} attempts and was moved to dead-letter state.")]
    private static partial void LogDeadLettered(ILogger logger, Exception ex, Guid messageId, string eventType, int attempts);
}
