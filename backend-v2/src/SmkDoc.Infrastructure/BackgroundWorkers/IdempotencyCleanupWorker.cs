using System.Data.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace SmkDoc.Infrastructure.BackgroundWorkers;

public sealed class IdempotencyCleanupOptions
{
    public const string SectionName = "IdempotencyCleanup";

    public int IntervalMinutes { get; set; } = 15;
    public int BatchSize { get; set; } = 500;
}

/// <summary>
/// Periodically purges expired PostgreSQL <c>idempotency_keys</c> rows in bounded batches.
/// </summary>
public sealed partial class IdempotencyCleanupWorker(
    NpgsqlDataSource dataSource,
    TimeProvider timeProvider,
    ILogger<IdempotencyCleanupWorker> logger,
    IOptions<IdempotencyCleanupOptions>? options = null) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(Math.Max(1, options?.Value.IntervalMinutes ?? 15));
    private readonly int _batchSize = Math.Clamp(options?.Value.BatchSize ?? 500, 50, 5000);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogWorkerStarted(logger, _interval.TotalMinutes, _batchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, timeProvider, stoppingToken);
                int totalDeleted = await PurgeExpiredAsync(stoppingToken);
                if (totalDeleted > 0 && logger.IsEnabled(LogLevel.Information))
                {
                    LogPurgedRows(logger, totalDeleted);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is DbException or TimeoutException or InvalidOperationException)
            {
                LogPurgeFailure(logger, ex);
            }
        }
    }

    internal async Task<int> PurgeExpiredAsync(CancellationToken ct)
    {
        int totalDeleted = 0;
        int deleted;
        do
        {
            await using var conn = await dataSource.OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT public.fn_purge_expired_idempotency_keys(@p_batch_size);";
            cmd.Parameters.Add(new NpgsqlParameter("p_batch_size", NpgsqlDbType.Integer) { Value = _batchSize });

            object? scalar = await cmd.ExecuteScalarAsync(ct);
            deleted = scalar is int count ? count : Convert.ToInt32(scalar, System.Globalization.CultureInfo.InvariantCulture);
            totalDeleted += deleted;
        }
        while (deleted >= _batchSize && !ct.IsCancellationRequested);

        return totalDeleted;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "IdempotencyCleanupWorker started. Interval={IntervalMinutes}m, BatchSize={BatchSize}")]
    private static partial void LogWorkerStarted(ILogger logger, double intervalMinutes, int batchSize);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Purged {DeletedCount} expired idempotency key rows.")]
    private static partial void LogPurgedRows(ILogger logger, int deletedCount);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed during expired idempotency key cleanup cycle.")]
    private static partial void LogPurgeFailure(ILogger logger, Exception ex);
}
